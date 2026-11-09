using System.Text.Json;
using System.Text.Json.Nodes;

namespace AcNote.Core;

public sealed class StyleLibrary(string bundled, string user)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private static JsonNode Parse(string json) => JsonNode.Parse(json,
        new JsonNodeOptions { PropertyNameCaseInsensitive = true },
        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
        ?? throw new InvalidOperationException("版式文件为空。");

    public bool IsBuiltIn(string id) => StyleCatalog.Load(bundled).All.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public StyleCatalog Load()
    {
        var scope = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(user).ToUpperInvariant())));
        using var mutex = new Mutex(false, "AcNote.StyleManagement." + scope);
        var held = false;
        try
        {
            try { held = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
            catch (AbandonedMutexException) { held = true; }
            if (!held) throw new InvalidOperationException("版式正在由另一程序更新，请稍后重试。");
            return LoadCore();
        }
        finally { if (held) mutex.ReleaseMutex(); }
    }

    private StyleCatalog LoadCore()
    {
        Directory.CreateDirectory(user);
        var originals = StyleCatalog.Load(bundled);
        var files = Directory.GetFiles(user, "*.style");
        var conflicts = files.Select(path => (Path: path, Node: Parse(File.ReadAllText(path))!.AsObject()))
            .Where(x => originals.All.Any(s => s.Id.Equals(x.Node["id"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))).ToList();
        if (conflicts.Count > 0)
        {
            // Older versions copied bundled styles into the user directory. Keep genuine
            // edits as independent snapshots before restoring the original bundled IDs.
            var old = StyleCatalog.Load(bundled, user);
            var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var conflict in conflicts)
            {
                var id = conflict.Node["id"]!.GetValue<string>();
                var original = originals.Get(id);
                var same = JsonNode.DeepEquals(conflict.Node, Parse(File.ReadAllText(original.FilePath!)));
                if (!same && !AppPaths.IsLegacySchoolPlaceholder(conflict.Node.ToJsonString()))
                {
                    var copyId = "custom-" + Guid.NewGuid().ToString("N");
                    var node = Snapshot(old.Get(id));
                    node["id"] = copyId;
                    node["name"] = old.Get(id).Name + "（旧版自定义）";
                    StyleCatalog.Install(node.ToJsonString(), user, bundled);
                    mapped[id] = copyId;
                }
            }
            // Preserve inheritance from an old customized parent, too.
            foreach (var path in files.Except(conflicts.Select(x => x.Path)))
            {
                var node = Parse(File.ReadAllText(path))!.AsObject();
                if (node["extends"]?.GetValue<string>() is string parent && mapped.TryGetValue(parent, out var replacement))
                {
                    node["extends"] = replacement;
                    File.Copy(path, path + ".before-management.bak", true);
                    File.WriteAllText(path, node.ToJsonString(Options));
                }
            }
            foreach (var conflict in conflicts)
                File.Move(conflict.Path, conflict.Path + ".before-management-" + Guid.NewGuid().ToString("N") + ".bak");
        }
        return StyleCatalog.Load(bundled, user);
    }

    public static JsonObject Snapshot(ResolvedStyle style) => new()
    {
        ["id"] = style.Id, ["name"] = style.Name, ["version"] = style.Version,
        ["layout"] = style.Layout, ["group"] = "custom", ["status"] = "custom",
        ["source"] = style.Source, ["notes"] = style.Notes,
        ["etAl"] = JsonSerializer.SerializeToNode(style.EtAl, Options),
        ["citation"] = JsonSerializer.SerializeToNode(new CitationStyle { EtAl = style.CitationEtAl, Format = style.CitationFormat, Punctuation = style.CitationPunctuation, RangeSeparator = style.CitationRangeSeparator }, Options),
        ["bibliographyFormat"] = JsonSerializer.SerializeToNode(style.BibliographyFormat, Options),
        ["bibliographyOptions"] = JsonSerializer.SerializeToNode(style.BibliographyOptions, Options),
        ["templates"] = JsonSerializer.SerializeToNode(style.Templates, Options),
        ["required"] = JsonSerializer.SerializeToNode(style.Required, Options),
    };

    private void CheckName(string name, string? except = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("请输入版式名称。");
        if (Load().All.Any(s => s.Id != except && s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("已有同名版式，请使用其他名称。");
    }

    public string CreateCopy(ResolvedStyle source, string name, string? editedJson = null)
    {
        CheckName(name);
        // Resolve edited templates against the complete source, so copies never depend
        // on another user style that could subsequently be removed.
        var node = Snapshot(source);
        if (editedJson is not null)
            foreach (var pair in Parse(editedJson)!.AsObject())
                if (pair.Key is not "id" and not "extends") node[pair.Key] = pair.Value?.DeepClone();
        node.Remove("extends");
        node["id"] = "custom-" + Guid.NewGuid().ToString("N");
        node["name"] = name.Trim(); node["group"] = "custom"; node["status"] = "custom";
        return StyleCatalog.Install(node.ToJsonString(Options), user, bundled);
    }

    public string Import(string json)
    {
        var temporary = Directory.CreateTempSubdirectory("acnote-style-import-");
        try
        {
            var parsed = Parse(json)!.AsObject();
            var id = parsed["id"]?.GetValue<string>() ?? throw new InvalidOperationException("版式缺少 id。");
            File.WriteAllText(Path.Combine(temporary.FullName, "import.style"), json);
            var source = StyleCatalog.Load(bundled, user, temporary.FullName).Get(id);
            var name = source.Name;
            var all = Load().All;
            for (var number = 1; all.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); number++)
                name = source.Name + (number == 1 ? "（导入）" : $"（导入 {number}）");
            return CreateCopy(source, name);
        }
        finally { temporary.Delete(true); }
    }

    public void Save(ResolvedStyle style, string json)
    {
        if (IsBuiltIn(style.Id)) throw new InvalidOperationException("内置版式只能另存为新的自定义版式。");
        var node = Parse(json)!.AsObject();
        node["id"] = style.Id; node["name"] = style.Name;
        node["group"] = "custom"; node["status"] = "custom";
        StyleCatalog.Install(node.ToJsonString(Options), user, bundled);
        RetireOldPath(style);
    }

    public void Rename(ResolvedStyle style, string name)
    {
        if (IsBuiltIn(style.Id)) throw new InvalidOperationException("内置版式不能重命名。");
        CheckName(name, style.Id);
        var node = Snapshot(style); node["name"] = name.Trim();
        StyleCatalog.Install(node.ToJsonString(Options), user, bundled);
        RetireOldPath(style);
    }

    private void RetireOldPath(ResolvedStyle style)
    {
        if (style.FilePath is not string path || !File.Exists(path)) return;
        var canonical = Path.Combine(user, style.Id + ".style");
        var root = Path.GetFullPath(user).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只能管理自定义版式目录内的文件。");
        if (!Path.GetFullPath(path).Equals(Path.GetFullPath(canonical), StringComparison.OrdinalIgnoreCase))
            File.Move(path, path + ".renamed-" + Guid.NewGuid().ToString("N") + ".bak");
    }

    public void Delete(ResolvedStyle style)
    {
        if (IsBuiltIn(style.Id)) throw new InvalidOperationException("内置版式不能删除。");
        var catalog = Load();
        foreach (var other in catalog.All.Where(s => s.Id != style.Id && !IsBuiltIn(s.Id)))
        {
            var node = Parse(File.ReadAllText(other.FilePath!));
            if (string.Equals(node?["extends"]?.GetValue<string>(), style.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"版式“{other.Name}”仍依赖此版式，请先将它另存为独立副本。");
        }
        var path = catalog.All.Single(s => s.Id == style.Id).FilePath!;
        if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(user).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只能删除自定义版式目录内的文件。");
        File.Delete(path);
    }
}
