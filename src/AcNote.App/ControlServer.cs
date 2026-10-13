using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AcNote.Core;
using AcNote.Word;

namespace AcNote.App;

public sealed class ControlServer : IDisposable
{
    private readonly Session _session;
    private readonly MainWindow _window;
    private readonly CancellationTokenSource _cancel = new();

    public ControlServer(Session session, MainWindow window)
    {
        _session = session;
        _window = window;
    }

    public void Start()
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = "AcNote.Pipe" };
        thread.Start();
    }

    public void Dispose()
    {
        _cancel.Cancel();
        try
        {
            using var pipe = new NamedPipeClientStream(".", ControlPipe.Name, PipeDirection.Out);
            pipe.Connect(200);
        }
        catch
        {
            /* No listener is waiting. */
        }
        _cancel.Dispose();
    }

    private void Loop()
    {
        while (!_cancel.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(ControlPipe.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                server.WaitForConnection();
            }
            catch
            {
                continue;
            }

            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                using var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
                writer.WriteLine(Handle(reader.ReadLine()));
            }
            catch
            {
                /* Accept the next connection. */
            }
        }
    }

    private string Handle(string? line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line))
                return Fail("空请求");

            using var document = JsonDocument.Parse(line);
            var command = document.RootElement.GetProperty("cmd").GetString();
            var form = document.RootElement.TryGetProperty("form", out var formValue) ? formValue.GetString() : null;
            return command switch
            {
                "ping" => "{\"ok\":true}",
                "open-files" => OnUi(() =>
                {
                    var paths = document.RootElement.GetProperty("paths").EnumerateArray().Select(p => p.GetString() ?? "").ToArray();
                    // Acknowledge receipt before importing so large files cannot block the sender.
                    _window.Dispatcher.BeginInvoke(new Action(() => _window.OpenFiles(paths)));
                    _window.WindowState = System.Windows.WindowState.Normal;
                    _window.Activate();
                    return "{\"ok\":true}";
                }),
                "activate" => OnUi(() =>
                {
                    _window.Activate();
                    _window.WindowState = System.Windows.WindowState.Normal;
                    return "{\"ok\":true}";
                }),
                "selected" => OnUi(() => Selected(form)),
                "pick" => OnUi(() => Pick(form, document.RootElement.TryGetProperty("ids", out var ids) ? ids.EnumerateArray().Select(id => id.GetString() ?? "").ToList() : [])),
                "choose-style" => OnUi(ChooseStyle),
                "edit-style" => OnUi(() => EditStyle(document.RootElement.TryGetProperty("styleId", out var style) ? style.GetString() : null)),
                "ai-status" => OnUi(AiStatus),
                "ai-create-library" => OnUi(() => AiCreateLibrary(Text(document.RootElement, "path"))),
                "ai-open-library" => OnUi(() => AiOpenLibrary(Text(document.RootElement, "path"))),
                "ai-import-references" => OnUi(() => AiImportReferences(Strings(document.RootElement, "paths"))),
                "ai-search-references" => OnUi(() => AiSearch(
                    Text(document.RootElement, "query"), Number(document.RootElement, "limit", 10))),
                "ai-get-references" => OnUi(() => AiGetReferences(Strings(document.RootElement, "ids"))),
                "ai-word-context" => OnUi(() => AiWordContext(Number(document.RootElement, "radius", 500))),
                "ai-insert-citation" => OnUi(() => AiInsertCitation(
                    Strings(document.RootElement, "ids"), Text(document.RootElement, "form", "parenthetical"),
                    Text(document.RootElement, "placement", "selection"), Text(document.RootElement, "anchor"),
                    Number(document.RootElement, "occurrence", 0))),
                "ai-update-citations" => OnUi(AiUpdateCitations),
                _ => Fail("未知命令"),
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private string Selected(string? form)
    {
        _window.SaveShown();
        var ids = _window.SelectedIds;
        if (ids.Count == 0) return Fail("请先在 AcNote 中选择文献（支持 Ctrl / Shift 多选），或使用“查找并插入”。");
        return JsonSerializer.Serialize(new { ok = true, ids, styleId = _session.StyleId, form = form ?? _window.SelectedForm, libraryPath = _session.LibraryPath });
    }

    private string Pick(string? form, IReadOnlyList<string> ids)
    {
        if (_session.Store is null)
            return Fail("请先在 AcNote 里打开一个文献库。");
        _window.SaveShown();
        var dialog = new PickerWindow(_session, form == "narrative" ? "narrative" : "parenthetical", ids) { Owner = _window };
        if (dialog.ShowDialog() != true)
            return "{\"ok\":false,\"cancelled\":true}";
        return JsonSerializer.Serialize(new { ok = true, ids = dialog.SelectedIds, styleId = _session.StyleId, form = dialog.SelectedForm, libraryPath = _session.LibraryPath });
    }

    private string EditStyle(string? styleId)
    {
        if (!string.IsNullOrWhiteSpace(styleId)) _session.StyleId = _session.Catalog.Get(styleId).Id;
        _window.EditCurrentStyle();
        return "{\"ok\":true}";
    }

    private string ChooseStyle()
    {
        var dialog = new StyleWindow(_session) { Owner = _window };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedStyleId))
            return "{\"ok\":false,\"cancelled\":true}";
        return JsonSerializer.Serialize(new { ok = true, styleId = dialog.SelectedStyleId });
    }

    private string AiStatus()
    {
        var word = RunningWord.TryGet();
        string? documentName = null;
        string? documentPath = null;
        if (word is not null)
        {
            try
            {
                dynamic app = word;
                documentName = (string)app.ActiveDocument.Name;
                documentPath = (string)app.ActiveDocument.FullName;
            }
            catch { /* Word is open without a document. */ }
        }
        return JsonSerializer.Serialize(new
        {
            ok = true,
            libraryOpen = _session.Store is not null,
            libraryPath = _session.LibraryPath,
            referenceCount = _session.Store?.Count() ?? 0,
            style = new { id = _session.StyleId, name = _session.CurrentStyle.Name },
            wordRunning = word is not null,
            documentName,
            documentPath,
        });
    }

    private string AiCreateLibrary(string path)
    {
        _window.CreateLibrary(path);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            created = true,
            libraryPath = _session.LibraryPath,
            referenceCount = _session.Store?.Count() ?? 0,
        });
    }

    private string AiOpenLibrary(string path)
    {
        _window.OpenLibrary(path);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            opened = true,
            libraryPath = _session.LibraryPath,
            referenceCount = _session.Store?.Count() ?? 0,
        });
    }

    private string AiImportReferences(IReadOnlyList<string> paths)
    {
        RequireStore();
        if (paths.Count == 0) return Fail("至少需要一个要导入的文献文件路径。");
        if (paths.Count > 20) return Fail("一次最多导入 20 个文献文件。");
        if (paths.Any(path => !Path.IsPathFullyQualified(path))) return Fail("所有导入文件都必须使用绝对路径。");
        if (paths.Any(path => Path.GetExtension(path).Equals(".acnote", StringComparison.OrdinalIgnoreCase)))
            return Fail("import_references 不接受 .acnote 文件；请使用 open_library。");
        var summary = _window.OpenFiles(paths.Select(Path.GetFullPath), showWarnings: false);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            allSucceeded = summary.Files.All(file => file.Error is null),
            partial = summary.Files.Any(file => file.Error is not null) && summary.Files.Any(file => file.Added > 0 || file.Updated > 0),
            summary.Added,
            summary.Updated,
            summary.LibraryCount,
            summary.Files,
            libraryPath = _session.LibraryPath,
        });
    }

    private string AiSearch(string query, int limit)
    {
        var store = RequireStore();
        if (string.IsNullOrWhiteSpace(query)) return Fail("检索词不能为空。");
        limit = Math.Clamp(limit, 1, 50);
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = store.List()
            .Select(item => new { Item = item, Text = SearchText(item) })
            .Where(x => terms.All(term => x.Text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .Select(x => ReferenceResult(x.Item));
        return JsonSerializer.Serialize(new { ok = true, query, results = matches });
    }

    private string AiGetReferences(IReadOnlyList<string> ids)
    {
        var store = RequireStore();
        if (ids.Count == 0) return Fail("至少需要一个文献 ID。");
        var missing = new List<string>();
        var records = new List<object>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            var item = store.Find(id);
            if (item is null) missing.Add(id);
            else records.Add(ReferenceResult(item));
        }
        return JsonSerializer.Serialize(new { ok = true, records, missingIds = missing });
    }

    private object ReferenceResult(ReferenceItem item) => new
    {
        item.Id,
        item.Type,
        item.Title,
        item.Authors,
        item.Year,
        item.ContainerTitle,
        item.Volume,
        item.Issue,
        item.Page,
        item.Publisher,
        item.PublisherPlace,
        item.Doi,
        item.Url,
        item.Language,
        item.SourceKey,
        item.Extra,
        formatted = BibliographyFormatter.Format(item, _session.CurrentStyle).Text,
        missingFields = MissingFieldChecker.Check(item, _session.CurrentStyle),
    };

    private string AiWordContext(int radius)
    {
        dynamic app = RequireWord();
        dynamic document = app.ActiveDocument;
        dynamic selection = app.Selection;
        radius = Math.Clamp(radius, 100, 2000);
        var start = (int)selection.Range.Start;
        var end = (int)selection.Range.End;
        var contentEnd = Math.Max(0, (int)document.Content.End - 1);
        var contextStart = Math.Max(0, start - radius);
        var contextEnd = Math.Min(contentEnd, end + radius);
        dynamic context = document.Range(contextStart, contextEnd);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            document = new { name = (string)document.Name, path = (string)document.FullName },
            selection = new { start, end, text = CleanWordText((string)selection.Range.Text) },
            context = new { start = contextStart, end = contextEnd, text = CleanWordText((string)context.Text) },
            boundLibraryPath = CitationDocument.GetLibraryPath(document),
            styleId = CitationDocument.GetStyleId(document),
        });
    }

    private string AiInsertCitation(IReadOnlyList<string> ids, string form, string placement, string anchor, int occurrence)
    {
        var store = RequireStore();
        if (ids.Count == 0) return Fail("至少需要一个文献 ID。");
        if (ids.Any(id => store.Find(id) is null)) return Fail("一个或多个文献 ID 不在当前文献库中。");
        if (form is not ("parenthetical" or "narrative")) return Fail("form 只能是 parenthetical 或 narrative。");
        if (placement is not ("selection" or "before" or "after")) return Fail("placement 只能是 selection、before 或 after。");

        dynamic app = RequireWord();
        dynamic document = app.ActiveDocument;
        dynamic selection = app.Selection;
        if (placement != "selection")
        {
            if (string.IsNullOrEmpty(anchor)) return Fail("按正文定位时必须提供 anchor。");
            var positions = ExactMatches((string)document.Content.Text, anchor);
            if (positions.Count == 0) return Fail("当前 Word 文档中没有找到完全一致的 anchor。");
            if (occurrence == 0 && positions.Count != 1) return Fail($"anchor 在文档中出现 {positions.Count} 次；请提供 occurrence 明确指定第几处。");
            var selectedOccurrence = occurrence == 0 ? 1 : occurrence;
            if (selectedOccurrence < 1 || selectedOccurrence > positions.Count) return Fail("occurrence 超出 anchor 出现次数。");
            var position = positions[selectedOccurrence - 1] + (placement == "after" ? anchor.Length : 0);
            selection.SetRange(position, position);
        }
        else if ((int)selection.Range.Start != (int)selection.Range.End)
        {
            return Fail("为避免覆盖正文，请把 Word 选择折叠为光标，或使用 before/after + anchor 定位。");
        }

        object wordDocument = document;
        var styleId = CitationDocument.GetStyleId(wordDocument);
        var style = _session.Catalog.Get(string.IsNullOrWhiteSpace(styleId) ? _session.StyleId : styleId);
        CitationDocument.BindLibrary(wordDocument, _session.LibraryPath!);
        CitationDocument.InsertAndRefresh(wordDocument, (object)selection, ids, form, style, store.Find);
        return JsonSerializer.Serialize(new { ok = true, insertedIds = ids, form, placement, document = (string)document.Name });
    }

    private string AiUpdateCitations()
    {
        var store = RequireStore();
        dynamic app = RequireWord();
        dynamic document = app.ActiveDocument;
        object wordDocument = document;
        var bound = CitationDocument.GetLibraryPath(wordDocument);
        if (string.IsNullOrWhiteSpace(bound)) CitationDocument.BindLibrary(wordDocument, _session.LibraryPath!);
        else if (!string.Equals(Path.GetFullPath(bound), Path.GetFullPath(_session.LibraryPath!), StringComparison.OrdinalIgnoreCase))
            return Fail("当前 Word 文档绑定的是另一份文献库，请先在 AcNote 中打开该库：" + bound);
        var styleId = CitationDocument.GetStyleId(wordDocument);
        var style = _session.Catalog.Get(string.IsNullOrWhiteSpace(styleId) ? _session.StyleId : styleId);
        CitationDocument.Refresh(wordDocument, style, store.Find);
        return JsonSerializer.Serialize(new { ok = true, document = (string)document.Name, styleId = style.Id });
    }

    private LibraryStore RequireStore() => _session.Store ?? throw new InvalidOperationException("请先在 AcNote 中打开文献库。");

    private static dynamic RequireWord() => RunningWord.TryGet() ?? throw new InvalidOperationException("请先打开 Word 文档。");

    private static string SearchText(ReferenceItem item) => string.Join('\n', new[]
    {
        item.Title, item.ContainerTitle, item.Year, item.Doi, item.Url, item.SourceKey,
        string.Join(' ', item.Authors.Select(author => $"{author.Family} {author.Given} {author.Literal}")),
        string.Join(' ', item.Extra.Select(pair => $"{pair.Key} {pair.Value}")),
    });

    private static List<int> ExactMatches(string text, string value)
    {
        var result = new List<int>();
        var start = 0;
        while (start <= text.Length - value.Length)
        {
            var index = text.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0) break;
            result.Add(index);
            start = index + Math.Max(1, value.Length);
        }
        return result;
    }

    private static string CleanWordText(string value) => value.Replace('\r', '\n').Replace("\a", "");

    private static string Text(JsonElement root, string name, string fallback = "") =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static int Number(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;

    private static List<string> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : [];

    private string OnUi(Func<string> action) => _window.Dispatcher.Invoke(action);

    private static string Fail(string message) => JsonSerializer.Serialize(new { ok = false, error = message });
}
