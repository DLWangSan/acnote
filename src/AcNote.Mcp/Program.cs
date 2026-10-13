using System.Text.Json;
using System.Text.Json.Nodes;
using AcNote.Core;

return await McpHost.RunAsync();

internal static class McpHost
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonNode? id = null;
            try
            {
                using var request = JsonDocument.Parse(line);
                var root = request.RootElement;
                if (root.TryGetProperty("id", out var idElement)) id = JsonNode.Parse(idElement.GetRawText());
                if (id is null) continue;
                var method = root.GetProperty("method").GetString();
                object result = method switch
                {
                    "initialize" => Initialize(root),
                    "ping" => new { },
                    "tools/list" => new { tools = Tools },
                    "tools/call" => CallTool(root),
                    _ => throw new McpException(-32601, "Method not found: " + method),
                };
                await WriteAsync(new { jsonrpc = "2.0", id, result });
            }
            catch (McpException ex)
            {
                if (id is not null) await WriteAsync(new { jsonrpc = "2.0", id, error = new { code = ex.Code, message = ex.Message } });
            }
            catch (Exception ex)
            {
                if (id is not null) await WriteAsync(new { jsonrpc = "2.0", id, error = new { code = -32603, message = ex.Message } });
            }
        }
        return 0;
    }

    private static object Initialize(JsonElement root)
    {
        var requested = root.TryGetProperty("params", out var p) && p.TryGetProperty("protocolVersion", out var version)
            ? version.GetString() : null;
        return new
        {
            protocolVersion = requested ?? "2025-06-18",
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = "acnote", version = typeof(McpHost).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" },
            instructions = "Use AcNote's currently open library. Read and verify references before inserting. Never guess reference IDs or ambiguous Word positions."
        };
    }

    private static object CallTool(JsonElement root)
    {
        var parameters = root.GetProperty("params");
        var name = parameters.GetProperty("name").GetString() ?? "";
        var arguments = parameters.TryGetProperty("arguments", out var args) ? args : default;
        var command = BuildCommand(name, arguments);
        var response = ControlPipe.Transact(JsonSerializer.Serialize(command, JsonOptions), attempts: 3, delayMs: 150);
        if (response is null)
            return ToolError("AcNote is not running. Start AcNote and open the literature library you want to use.");
        using var parsed = JsonDocument.Parse(response);
        var ok = parsed.RootElement.TryGetProperty("ok", out var okValue) && okValue.GetBoolean();
        if (!ok)
        {
            var message = parsed.RootElement.TryGetProperty("error", out var error) ? error.GetString() : "AcNote rejected the request.";
            return ToolError(message ?? "AcNote rejected the request.");
        }
        return new
        {
            content = new[] { new { type = "text", text = response } },
            structuredContent = JsonNode.Parse(response),
            isError = false,
        };
    }

    private static object BuildCommand(string name, JsonElement args) => name switch
    {
        "get_status" => new { cmd = "ai-status" },
        "create_library" => new { cmd = "ai-create-library", path = StringArg(args, "path") },
        "open_library" => new { cmd = "ai-open-library", path = StringArg(args, "path") },
        "import_references" => new { cmd = "ai-import-references", paths = StringArrayArg(args, "paths") },
        "search_references" => new { cmd = "ai-search-references", query = StringArg(args, "query"), limit = IntArg(args, "limit", 10) },
        "get_references" => new { cmd = "ai-get-references", ids = StringArrayArg(args, "ids") },
        "get_word_context" => new { cmd = "ai-word-context", radius = IntArg(args, "radius", 500) },
        "insert_citation" => new { cmd = "ai-insert-citation", ids = StringArrayArg(args, "ids"), form = StringArg(args, "form", "parenthetical"), placement = StringArg(args, "placement", "selection"), anchor = StringArg(args, "anchor"), occurrence = IntArg(args, "occurrence", 0) },
        "update_citations" => new { cmd = "ai-update-citations" },
        _ => throw new McpException(-32602, "Unknown tool: " + name),
    };

    private static string StringArg(JsonElement args, string name, string fallback = "") =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static int IntArg(JsonElement args, string name, int fallback) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;

    private static string[] StringArrayArg(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray() : [];

    private static object ToolError(string message) => new { content = new[] { new { type = "text", text = message } }, isError = true };

    private static async Task WriteAsync(object value)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));
        await Console.Out.FlushAsync();
    }

    private static readonly object[] Tools =
    [
        Tool("get_status", "Check AcNote, its current library and the active Word document before a reference workflow.", new { type = "object", properties = new { }, additionalProperties = false }, true),
        Tool("create_library", "Create and open a new AcNote library at an absolute .acnote path. Refuses to overwrite an existing library or companion files.", new { type = "object", properties = new { path = new { type = "string", description = "Absolute path ending in .acnote." } }, required = new[] { "path" }, additionalProperties = false }, false),
        Tool("open_library", "Open an existing .acnote library as AcNote's current library.", new { type = "object", properties = new { path = new { type = "string", description = "Absolute path to an existing .acnote file." } }, required = new[] { "path" }, additionalProperties = false }, false),
        Tool("import_references", "Import up to 20 EndNote, BibTeX, RIS, CSL JSON, or Zotero files into AcNote's current library. Returns per-file added, updated, warning, and error details.", new { type = "object", properties = new { paths = new { type = "array", items = new { type = "string" }, minItems = 1, maxItems = 20, description = "Absolute source file paths. EndNote .enl files must retain their matching .Data directories." } }, required = new[] { "paths" }, additionalProperties = false }, false),
        Tool("search_references", "Search the current AcNote library by title, author, year, journal, DOI, URL or source key. Returns stable AcNote reference IDs and formatted previews.", new { type = "object", properties = new { query = new { type = "string" }, limit = new { type = "integer", minimum = 1, maximum = 50, @default = 10 } }, required = new[] { "query" }, additionalProperties = false }, true),
        Tool("get_references", "Read exact records from the current AcNote library by stable IDs and check missing fields for the current style.", new { type = "object", properties = new { ids = new { type = "array", items = new { type = "string" }, minItems = 1, maxItems = 50 } }, required = new[] { "ids" }, additionalProperties = false }, true),
        Tool("get_word_context", "Inspect the active Word document and current selection, including nearby text and AcNote binding. Use before choosing an insertion position.", new { type = "object", properties = new { radius = new { type = "integer", minimum = 100, maximum = 2000, @default = 500 } }, additionalProperties = false }, true),
        Tool("insert_citation", "Insert an AcNote citation without replacing prose. Use placement selection for the current cursor, or before/after with an exact anchor. Ambiguous anchors are rejected unless occurrence is explicitly supplied.", new { type = "object", properties = new { ids = new { type = "array", items = new { type = "string" }, minItems = 1, maxItems = 20 }, form = new { type = "string", @enum = new[] { "parenthetical", "narrative" }, @default = "parenthetical" }, placement = new { type = "string", @enum = new[] { "selection", "before", "after" }, @default = "selection" }, anchor = new { type = "string", description = "Exact Word text used only with before/after." }, occurrence = new { type = "integer", minimum = 0, description = "1-based anchor occurrence. Leave 0 to require a unique anchor." } }, required = new[] { "ids" }, additionalProperties = false }, false),
        Tool("update_citations", "Refresh all AcNote citations and the bibliography in the active Word document using its bound library and style.", new { type = "object", properties = new { }, additionalProperties = false }, false),
    ];

    private static object Tool(string name, string description, object inputSchema, bool readOnly) => new { name, title = name.Replace('_', ' '), description, inputSchema, annotations = new { readOnlyHint = readOnly, destructiveHint = false, openWorldHint = false } };

    private sealed class McpException(int code, string message) : Exception(message) { public int Code { get; } = code; }
}
