using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using System.Text;
using AcNote.Core;
using AcNote.Word;
using AcNote.Import.EndNote;
using Microsoft.Win32;

namespace AcNote.WordAddin;

[ComVisible(true)]
[Guid("C4A1E0B7-7E2D-4C5A-9F10-6D2B8A1E0C33")]
[ProgId("AcNote.Connect")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IDTExtensibility2))]
public sealed class Connect : IDTExtensibility2, IRibbonExtensibility
{
    private const string Ribbon = """
        <customUI xmlns="http://schemas.microsoft.com/office/2009/07/customui" onLoad="OnRibbonLoad">
          <ribbon>
            <tabs>
              <tab id="tabAcNote" label="AcNote">
                <group id="grpCitations" label="引用">
                  <button id="btnInsertSelected" label="插入已选文献" size="large" imageMso="CitationInsert" screentip="插入主程序中已选的文献" supertip="在 AcNote 文献库中选择一篇或多篇文献，返回 Word 后点击这里直接插入，无需再次选择。" onAction="OnInsertCitation"/>
                  <button id="btnFind" label="查找并插入" size="large" imageMso="FindDialog" onAction="OnFindCitation"/>
                  <button id="btnConvertEndNote" label="存为 AcNote 版副本" size="large" imageMso="FileSaveAs" supertip="保留原文档，在同目录创建 AcNote 副本并转换可读取的 EndNote 引用。" onAction="OnConvertEndNoteCopy"/>
                  <button id="btnEdit" label="编辑引用" imageMso="SourceManager" onAction="OnEditCitation"/>
                  <button id="btnRemove" label="移除此处引用" imageMso="Delete" onAction="OnRemoveCitation"/>
                  <menu id="mInsert" label="引用形式" imageMso="CitationInsert">
                    <button id="btnConvertNarr" label="此处改为：作者（年份）" onAction="OnConvertNarrative"/>
                    <button id="btnConvertParen" label="此处改为：（作者，年份）" onAction="OnConvertParenthetical"/>
                    <button id="btnConvertDefault" label="此处恢复为当前版式" onAction="OnConvertDefault"/>
                    <button id="btnParen" label="插入已选文献：（作者，年）" onAction="OnInsertParenthetical"/>
                    <button id="btnNarr" label="插入已选文献：作者（年）" onAction="OnInsertNarrative"/>
                  </menu>
                </group>
                <group id="grpLibrary" label="文献库">
                  <button id="btnOpen" label="转到 AcNote" imageMso="FileOpen" size="large" onAction="OnOpenApp"/>
                  <button id="btnGithub" label="GitHub 项目" imageMso="HyperlinkInsert" onAction="OnOpenGithub"/>
                </group>
                <group id="grpBibliography" label="参考文献">
                  <button id="btnUpdate" label="更新引用和列表" size="large" imageMso="Refresh" supertip="按文中首次出现顺序重新编号，同时更新参考文献列表。" onAction="OnUpdateCitations"/>
                  <dropDown id="ddStyle" label="版式" showLabel="true" sizeString="GB/T 7714-2015 顺序编码制" getItemCount="StyleCount" getItemLabel="StyleLabel" getSelectedItemIndex="StyleSelected" onAction="StyleChosen"/>
                  <button id="btnEditStyle" label="字体、段落与版式" imageMso="StyleInspector" onAction="OnEditStyle"/>
                </group>
              </tab>
            </tabs>
          </ribbon>
          <contextMenus>
            <contextMenu idMso="ContextMenuText">
              <menu id="acnoteContext" label="AcNote 引用形式">
                <button id="ctxNarrative" label="作者（年份）" onAction="OnConvertNarrative"/>
                <button id="ctxParenthetical" label="（作者，年份）" onAction="OnConvertParenthetical"/>
                <button id="ctxDefault" label="恢复为当前版式" onAction="OnConvertDefault"/>
                <button id="ctxRemove" label="移除此处引用" onAction="OnRemoveCitation"/>
              </menu>
            </contextMenu>
          </contextMenus>
        </customUI>
        """;

    private object? _application;
    private object? _ribbon;

    public void OnRibbonLoad(object ribbon)
    {
        _ribbon = ribbon;
        Log("ribbon-loaded " + (ribbon is null ? "no-ribbon" : ribbon.GetType().FullName));
    }

    public void OnConnection(
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object application,
        ext_ConnectMode connectMode,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object addInInst,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom)
    {
        _application = application;
        Log(_application is null ? "connected no-app" : "connected app");
        Log("addin-build " + typeof(Connect).Module.ModuleVersionId + " path=" + typeof(Connect).Assembly.Location);
        Log("citation-build " + typeof(CitationDocument).Module.ModuleVersionId + " path=" + typeof(CitationDocument).Assembly.Location);
        try
        {
            AppPaths.EnsureStyles(BundledStyles());
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
        }
    }

    public void OnDisconnection(ext_DisconnectMode removeMode, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom) => _application = null;

    public void OnAddInsUpdate([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom) { }

    public void OnStartupComplete([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom) => Log("startup");

    public void OnBeginShutdown([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom) { }

    public string GetCustomUI(string ribbonId)
    {
        try
        {
            Log("ribbon " + ribbonId);
            return Ribbon;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return Ribbon;
        }
    }

    public void OnFindCitation(object control) => InsertCitation(null, true);

    public void OnInsertCitation(object control) => InsertCitation(null);

    public void OnInsertParenthetical(object control) => InsertCitation("parenthetical");

    public void OnInsertNarrative(object control) => InsertCitation("narrative");

    private void InsertCitation(string? form, bool find = false) => Guard(() =>
    {
        var (document, selection) = Active();
        var request = JsonSerializer.Serialize(new { cmd = find ? "pick" : "selected", form });
        var picked = Ask(request);
        if (!picked.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return;
        var ids = picked.GetProperty("ids").EnumerateArray().Select(item => item.GetString() ?? "").Where(id => id.Length > 0).ToList();
        var style = ResolveStyle(document, picked);
        CitationDocument.BindLibrary(document, picked.GetProperty("libraryPath").GetString()!);
        CitationDocument.InsertAndRefresh(document, selection, ids, FormOf(picked), style, LookupFor(document));
        InvalidateStyle();
    });

    public void OnEditCitation(object control) => Guard(() =>
    {
        var (document, selection) = Active();
        if (!CitationDocument.SelectionIsCitation(selection))
        {
            Dialogs.Info("请把光标放在一处 AcNote 引用里。");
            return;
        }

        var current = CitationDocument.SelectionForm(selection);
        var picked = Ask(JsonSerializer.Serialize(new { cmd = "pick", form = current, ids = CitationDocument.SelectionIds(selection) }));
        if (!picked.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return;
        var ids = picked.GetProperty("ids").EnumerateArray().Select(item => item.GetString() ?? "").Where(id => id.Length > 0).ToList();
        var style = ResolveStyle(document, picked);
        CitationDocument.BindLibrary(document, picked.GetProperty("libraryPath").GetString()!);
        CitationDocument.ReplaceSelectedAndRefresh(document, selection, ids, FormOf(picked), style, LookupFor(document));
    });

    public void OnUpdateCitations(object control) => Guard(() =>
    {
        var (document, _) = Active();
        CitationDocument.Refresh(document, ResolveStyle(document, null), LookupFor(document));
        InvalidateStyle();
    });

    public void OnRemoveCitation(object control) => Guard(() =>
    {
        var (document, selection) = Active();
        CitationDocument.RemoveSelectedAndRefresh(document, selection, ResolveStyle(document, null), LookupFor(document));
    });

    public void OnOpenApp(object control) => Guard(() => Ask("{\"cmd\":\"activate\"}"));

    public void OnOpenGithub(object control) => Guard(() => Process.Start(new ProcessStartInfo("https://github.com/DLWangSan/acnote") { UseShellExecute = true }));

    public void OnConvertEndNoteCopy(object control) => Guard(ConvertEndNoteCopy);

    private void ConvertEndNoteCopy()
    {
        var (source, _) = Active();
        dynamic app = _application!;
        dynamic original = source;
        var sourcePath = (string)original.FullName;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException("请先保存原 Word 文档，再创建 AcNote 副本。");

        var fields = new List<(int Start, int End, string Code, bool IsBibliography, string ResultText)>();
        var travelingXml = new StringBuilder();
        dynamic wordFields = original.Fields;
        Log("endnote-copy scan-start path=" + sourcePath);
        for (var i = 1; i <= (int)wordFields.Count; i++)
        {
            dynamic field = wordFields[i];
            var code = (string)field.Code.Text;
            var upper = code.TrimStart();
            var isData = upper.StartsWith("ADDIN EN.CITE.DATA", StringComparison.OrdinalIgnoreCase);
            var isCite = !isData && upper.StartsWith("ADDIN EN.CITE", StringComparison.OrdinalIgnoreCase);
            var isBibliography = upper.StartsWith("ADDIN EN.REFLIST", StringComparison.OrdinalIgnoreCase);
            if (isCite || isData) travelingXml.AppendLine(System.Net.WebUtility.HtmlDecode(code));
            if (isCite || isBibliography)
                fields.Add(((int)field.Result.Start, (int)field.Result.End, code, isBibliography, (string)field.Result.Text));
        }

        var imported = EndNoteImporter.ImportTravelingLibrary(travelingXml.ToString());
        var byRec = imported.Items
            .Where(item => item.SourceKey.StartsWith("endnote-xml:", StringComparison.Ordinal))
            .GroupBy(item => item.SourceKey["endnote-xml:".Length..])
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var citationRecords = fields.Where(field => !field.IsBibliography).SelectMany(field => RecordNumbers(field.Code)).Distinct(StringComparer.Ordinal).ToArray();
        Log($"endnote-copy scan-complete fields={fields.Count} records={imported.Items.Count} citeRecords={citationRecords.Length} embeddedXmlChars={travelingXml.Length}");
        var unresolvedVisibleCitations = fields.Where(field => !field.IsBibliography && RecordNumbers(field.Code).Count == 0)
            .Select(field => EndNoteImporter.MatchUnformattedCitationText(field.ResultText, byRec.Values))
            .Any(matches => matches.Count == 0 || matches.Any(match => match.Reference is null));
        if (byRec.Count == 0 || citationRecords.Any(record => !byRec.ContainsKey(record)) || unresolvedVisibleCitations)
        {
            using var picker = new OpenFileDialog { Title = "选择对应的 EndNote 文献库（.enl / .enlx / XML）", Filter = "EndNote 文献库|*.enl;*.enlx;*.xml|所有文件|*.*" };
            if (picker.ShowDialog() != DialogResult.OK) return;
            var fromLibrary = EndNoteImporter.Import(picker.FileName);
            Log($"endnote-copy selected-library path={picker.FileName} records={fromLibrary.Items.Count} warnings={fromLibrary.Warnings.Count}");
            imported.Items.AddRange(fromLibrary.Items);
            imported.Warnings.AddRange(fromLibrary.Warnings);
            foreach (var record in fromLibrary.Items
                .Where(item => item.SourceKey.StartsWith("endnote-xml:", StringComparison.Ordinal) || item.SourceKey.StartsWith("endnote-enl:", StringComparison.Ordinal))
                )
            {
                var key = record.SourceKey[(record.SourceKey.IndexOf(':') + 1)..];
                byRec[key] = record;
            }
            imported.Items.Clear();
            imported.Items.AddRange(byRec.Values);
        }
        if (imported.Items.Count == 0) throw new InvalidDataException("没有从文档或所选 EndNote 库中读取到文献记录。");
        var missingRecords = citationRecords.Where(record => !byRec.ContainsKey(record)).ToArray();
        if (missingRecords.Length > 0)
            Log("endnote-copy unresolved-record-numbers=" + string.Join(",", missingRecords));

        var suffix = "-AcNote";
        var directory = Path.GetDirectoryName(sourcePath)!;
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var copyPath = Path.Combine(directory, stem + suffix + extension);
        var libraryPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(copyPath) + ".acnote");
        for (var n = 2; File.Exists(copyPath) || File.Exists(libraryPath); n++)
        {
            copyPath = Path.Combine(directory, stem + suffix + " (" + n + ")" + extension);
            libraryPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(copyPath) + ".acnote");
        }
        var store = new LibraryStore(libraryPath);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in imported.Items)
        {
            var key = item.SourceKey[(item.SourceKey.IndexOf(':') + 1)..];
            if (store.FindBySource(item.SourceKey) is { } existing) { ids[key] = existing.Id; continue; }
            store.Save(item);
            ids[key] = item.Id;
        }
        var citationPlans = fields.Where(field => !field.IsBibliography)
            .ToDictionary(field => field.Start, field => ResolveCitation(field.Code, field.ResultText, imported.Items, ids));
        var missingCitations = citationPlans.Values.SelectMany(plan => plan.Missing).Distinct(StringComparer.Ordinal).ToList();
        Log($"endnote-copy citation-resolution resolved={citationPlans.Values.Sum(plan => plan.Ids.Count)} missing={missingCitations.Count}");
        foreach (var field in fields.Where(field => !field.IsBibliography))
            if (citationPlans[field.Start].Missing.Count > 0)
                Log($"endnote-copy unmatched position={field.Start} text={field.ResultText.Trim()} missing={string.Join("; ", citationPlans[field.Start].Missing)}");

        // Word's Document object has no SaveCopyAs method (that method belongs to
        // Excel workbooks). Save the current document first, then copy its on-disk
        // package so unsaved edits and embedded EndNote traveling-library data are
        // both retained without changing the document's identity/path in Word.
        dynamic converted;
        try
        {
            original.Save();
            File.Copy(sourcePath, copyPath);
            Log("endnote-copy file-copied " + copyPath);
            converted = app.Documents.Open(copyPath, ReadOnly: false, AddToRecentFiles: true);
        }
        catch (Exception ex)
        {
            try { File.Delete(copyPath); } catch { }
            try { File.Delete(libraryPath); } catch { }
            throw new InvalidOperationException($"无法创建并打开 AcNote 副本（{ex.Message}）。原文档未被转换。", ex);
        }
        try
        {
            // Process from the end so field removal cannot shift the remaining Word ranges.
            foreach (var entry in fields.OrderByDescending(field => field.Start))
            {
                if (entry.IsBibliography)
                {
                    dynamic bibliographyFields = converted.Fields;
                    for (var j = (int)bibliographyFields.Count; j >= 1; j--)
                    {
                        dynamic f = bibliographyFields[j];
                        if (((string)f.Code.Text).TrimStart().StartsWith("ADDIN EN.REFLIST", StringComparison.OrdinalIgnoreCase))
                        { f.Result.Text = ""; f.Unlink(); }
                    }
                    continue;
                }
                var plan = citationPlans[entry.Start];
                // Unlink leaves the rendered citation text in the document; replace only its field wrapper.
                dynamic matchField = null!;
                for (var j = (int)converted.Fields.Count; j >= 1; j--)
                {
                    dynamic f = converted.Fields[j];
                    if ((int)f.Result.Start == entry.Start && ((string)f.Code.Text).TrimStart().StartsWith("ADDIN EN.CITE", StringComparison.OrdinalIgnoreCase)) { matchField = f; break; }
                }
                if (matchField is null) continue;
                matchField.Unlink();
                dynamic fieldRange = converted.Range(entry.Start, entry.End);
                if (plan.Ids.Count == 0)
                {
                    WriteMissingCitationMarker(fieldRange, FormatMissingCitationMarker(plan.Missing, entry.ResultText));
                    continue;
                }
                dynamic control = converted.ContentControls.Add(0, fieldRange);
                CitationDocument.PrepareForConversion(control, CitationDocument.BuildTag("parenthetical", plan.Ids));
                if (plan.Missing.Count > 0)
                {
                    dynamic markerRange = converted.Range((int)control.Range.End, (int)control.Range.End);
                    WriteMissingCitationMarker(markerRange, FormatMissingCitationMarker(plan.Missing, entry.ResultText));
                }
            }
            CitationDocument.BindLibrary(converted, libraryPath);
            CitationDocument.SetStyleId(converted, Catalog().All.FirstOrDefault()?.Id ?? "");
            CitationDocument.Refresh(converted, Catalog().Get(CitationDocument.GetStyleId(converted)), LookupFor(converted));
            converted.Save();
        }
        catch
        {
            try { converted.Close(SaveChanges: false); } catch { }
            try { File.Delete(copyPath); } catch { }
            try { File.Delete(libraryPath); } catch { }
            throw;
        }
        var summary = new StringBuilder($"已创建 AcNote 副本：\n{copyPath}\n\n已导入 {store.Count()} 条文献记录。原文档仍保留 EndNote 引用。");
        if (missingCitations.Count > 0)
        {
            summary.AppendLine().AppendLine().AppendLine("以下引用未能在所选文献库中唯一匹配，副本中已在原位置标红标注：");
            foreach (var missing in missingCitations) summary.AppendLine("• " + missing);
        }
        else
        {
            summary.AppendLine().AppendLine().Append("所有文中引用均已匹配。");
        }
        summary.AppendLine().AppendLine("若需迁移文档未引用的其他记录，请另行导入完整的 .enl 文献库。");
        Dialogs.Info(summary.ToString());
    }

    private static List<string> RecordNumbers(string fieldCode) => EndNoteImporter.ExtractCitationRecordNumbers(fieldCode);

    private static (List<string> Ids, List<string> Missing) ResolveCitation(
        string fieldCode, string renderedText, IReadOnlyList<ReferenceItem> references, IReadOnlyDictionary<string, string> ids)
    {
        var keys = RecordNumbers(fieldCode);
        var missing = new List<string>();
        var citeIds = new List<string>();
        if (keys.Count > 0 && keys.All(ids.ContainsKey))
            citeIds.AddRange(keys.Select(key => ids[key]));
        else
        {
            var matches = EndNoteImporter.MatchUnformattedCitationText(renderedText, references);
            if (matches.Count > 0)
            {
                foreach (var match in matches)
                {
                    if (match.Reference is null)
                    {
                        missing.Add(match.CandidateCount > 1
                            ? $"{match.Citation}（同作者同年有 {match.CandidateCount} 条记录，无法确定）"
                            : $"{match.Citation}（文献库中未找到）");
                        continue;
                    }
                    var key = match.Reference.SourceKey[(match.Reference.SourceKey.IndexOf(':') + 1)..];
                    if (ids.TryGetValue(key, out var id)) citeIds.Add(id);
                    else missing.Add($"{match.Citation}（记录未导入）");
                }
            }
            else if (keys.Count > 0)
            {
                foreach (var key in keys)
                {
                    if (ids.TryGetValue(key, out var id)) citeIds.Add(id);
                    else missing.Add($"EndNote 记录号 #{key}（文献库中未找到）");
                }
            }
            else
            {
                missing.Add(string.IsNullOrWhiteSpace(renderedText) ? "位置未知的 EndNote 引用" : $"{renderedText.Trim()}（无法识别作者和年份）");
            }
        }
        return (citeIds.Distinct(StringComparer.Ordinal).ToList(), missing.Distinct(StringComparer.Ordinal).ToList());
    }

    private static string FormatMissingCitationMarker(IReadOnlyList<string> missing, string originalText) =>
        "【AcNote未匹配引用：" + (missing.Count > 0 ? string.Join("；", missing) : originalText.Trim()) + "】";

    private static void WriteMissingCitationMarker(dynamic range, string marker)
    {
        range.Text = marker;
        range.Font.Color = 255;
        range.Font.Bold = true;
    }

    public void OnConvertNarrative(object control) => ConvertCitation("narrative");
    public void OnConvertParenthetical(object control) => ConvertCitation("author-year");
    public void OnConvertDefault(object control) => ConvertCitation("parenthetical");

    private void ConvertCitation(string form) => Guard(() =>
    {
        var (document, selection) = Active();
        if (!CitationDocument.SelectionIsCitation(selection))
            throw new InvalidOperationException("请选中一处 AcNote 引用或把光标放在引用内。");
        CitationDocument.ReplaceSelectedAndRefresh(document, selection, CitationDocument.SelectionIds(selection),
            form, ResolveStyle(document, null), LookupFor(document));
    });

    public void OnEditStyle(object control) => Guard(() =>
    {
        var (document, _) = Active();
        var previous = CitationDocument.GetTypography(document);
        using var dialog = new TypographyDialog(previous);
        if (dialog.ShowForWord(_application!) != System.Windows.Forms.DialogResult.OK) return;
        var lookup = LookupFor(document);
        CitationDocument.SetTypography(document, dialog.Value);
        try { CitationDocument.Refresh(document, ResolveStyle(document, null), lookup); }
        catch { CitationDocument.SetTypography(document, previous); throw; }
        InvalidateStyle();
    });

    public int StyleCount(object control) => Catalog().All.Count;

    public string StyleLabel(object control, int index)
    {
        var styles = Catalog().All;
        return index >= 0 && index < styles.Count ? styles[index].DisplayName : "";
    }

    public int StyleSelected(object control)
    {
        try
        {
            var (document, _) = Active();
            var id = CitationDocument.GetStyleId(document);
            var styles = Catalog().All;
            for (var index = 0; index < styles.Count; index++)
            {
                if (string.Equals(styles[index].Id, id, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
        }
        catch (Exception ex)
        {
            Log(ex.Message);
        }

        return 0;
    }

    public void StyleChosen(object control, string selectedId, int selectedIndex) => Guard(() =>
    {
        var styles = Catalog().All;
        if (selectedIndex < 0 || selectedIndex >= styles.Count)
            return;
        var style = styles[selectedIndex];
        var (document, _) = Active();
        CitationDocument.Refresh(document, style, LookupFor(document));
        CitationDocument.SetStyleId(document, style.Id);
        InvalidateStyle();
    });

    private (object Document, object Selection) Active()
    {
        if (_application is null)
            throw new InvalidOperationException("AcNote 还没有连上 Word。");
        dynamic application = _application;
        return (application.ActiveDocument, application.Selection);
    }

    private static ResolvedStyle ResolveStyle(object document, JsonElement? picked)
    {
        var id = CitationDocument.GetStyleId(document);
        if (string.IsNullOrWhiteSpace(id) && picked is JsonElement element && element.TryGetProperty("styleId", out var styleId))
            id = styleId.GetString();
        return Catalog().Get(id);
    }

    private static string BundledStyles()
    {
        var location = typeof(Connect).Assembly.Location;
        var directory = string.IsNullOrWhiteSpace(location) ? AppContext.BaseDirectory : Path.GetDirectoryName(location)!;
        return Path.Combine(directory, "styles");
    }

    private static StyleCatalog Catalog() => StyleCatalog.Load(BundledStyles(), AppPaths.Styles);

    private static Func<string, ReferenceItem?> LookupFor(object document)
    {
        var path = CitationDocument.GetLibraryPath(document);
        if (string.IsNullOrWhiteSpace(path)) path = WorkspaceSettings.LibraryPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("请先在 AcNote 里打开一个文献库。");
        var records = new LibraryStore(path).List().ToDictionary(item => item.Id);
        return id => records.GetValueOrDefault(id);
    }

    private static string FormOf(JsonElement picked) =>
        picked.TryGetProperty("form", out var form) && form.GetString() == "narrative"
            ? "narrative"
            : "parenthetical";

    private static JsonElement Ask(string request)
    {
        var answer = ControlPipe.Transact(request, attempts: 1, delayMs: 0);
        if (answer is null)
        {
            var exe = Registry.GetValue(@"HKEY_CURRENT_USER\Software\AcNote", "AppPath", null) as string;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                throw new InvalidOperationException("请先运行 AcNote 主程序。");
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            answer = ControlPipe.Transact(request, attempts: 40, delayMs: 250);
        }

        if (answer is null)
            throw new InvalidOperationException("AcNote 主程序没有响应。");

        using var document = JsonDocument.Parse(answer);
        if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
        {
            var message = error.GetString();
            if (!string.IsNullOrWhiteSpace(message) && document.RootElement.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
                throw new InvalidOperationException(message);
        }
        return document.RootElement.Clone();
    }

    private void InvalidateStyle()
    {
        try
        {
            if (_ribbon is null)
                return;
            dynamic ribbon = _ribbon;
            ribbon.InvalidateControl("ddStyle");
        }
        catch (Exception ex)
        {
            Log(ex.Message);
        }
    }

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            Dialogs.Error(ex.Message);
        }
    }

    internal static void Log(string message)
    {
        var line = DateTime.Now.ToString("s") + " " + message + Environment.NewLine;
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.AppendAllText(AppPaths.AddinLog, line, Encoding.UTF8);
        }
        catch
        {
            /* The temp log still records the line when AppData cannot be written. */
        }

        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "acnote-word.log"), line, Encoding.UTF8);
        }
        catch
        {
            /* Logging must not affect citations. */
        }
    }
}

public sealed class VariantDump : ICustomMarshaler
{
    private readonly bool _dispatch;

    public VariantDump(string cookie) => _dispatch = cookie == "dispatch";

    public static ICustomMarshaler GetInstance(string cookie) => new VariantDump(cookie);

    public object MarshalNativeToManaged(IntPtr native)
    {
        if (!_dispatch || native == IntPtr.Zero)
            return null!;
        try
        {
            return Marshal.GetObjectForIUnknown(native);
        }
        catch (Exception ex)
        {
            Connect.Log("dispatch " + ex.GetType().Name);
            return null!;
        }
    }

    public IntPtr MarshalManagedToNative(object managed) => IntPtr.Zero;
    public void CleanUpNativeData(IntPtr native) { }
    public void CleanUpManagedData(object managed) { }
    public int GetNativeDataSize() => -1;
}

public static class Dialogs
{
    public static void Info(string text) => Show(text, 0x40);
    public static void Error(string text) => Show(text, 0x10);

    private static void Show(string text, uint icon) => MessageBox(IntPtr.Zero, text, "AcNote", icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}

public enum ext_ConnectMode
{
    ext_cm_AfterStartup = 0,
    ext_cm_Startup = 1,
    ext_cm_External = 2,
    ext_cm_CommandLine = 3,
    ext_cm_Solution = 4,
    ext_cm_UISetup = 5,
}

public enum ext_DisconnectMode
{
    ext_dm_HostShutdown = 0,
    ext_dm_UserClosed = 1,
    ext_dm_UISetupComplete = 2,
    ext_dm_SolutionClosed = 3,
}

[ComImport]
[ComVisible(true)]
[Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IDTExtensibility2
{
    [DispId(1)]
    void OnConnection(
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object application,
        ext_ConnectMode connectMode,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object addInInst,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom);

    [DispId(2)]
    void OnDisconnection(ext_DisconnectMode removeMode, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom);

    [DispId(3)]
    void OnAddInsUpdate([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom);

    [DispId(4)]
    void OnStartupComplete([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom);

    [DispId(5)]
    void OnBeginShutdown([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom);

    [DispId(10)]
    void OnRibbonLoad(object ribbon);

    [DispId(11)]
    void OnInsertCitation(object control);

    [DispId(12)]
    void OnInsertParenthetical(object control);

    [DispId(13)]
    void OnInsertNarrative(object control);

    [DispId(14)]
    void OnEditCitation(object control);

    [DispId(15)]
    void OnUpdateCitations(object control);

    [DispId(16)]
    void OnOpenApp(object control);

    [DispId(27)]
    void OnOpenGithub(object control);

    [DispId(28)]
    void OnConvertEndNoteCopy(object control);

    [DispId(17)]
    void OnEditStyle(object control);

    [DispId(18)]
    int StyleCount(object control);

    [DispId(19)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string StyleLabel(object control, int index);

    [DispId(20)]
    int StyleSelected(object control);

    [DispId(23)]
    void OnRemoveCitation(object control);

    [DispId(24)]
    void OnConvertNarrative(object control);

    [DispId(25)]
    void OnConvertParenthetical(object control);

    [DispId(26)]
    void OnConvertDefault(object control);

    [DispId(22)]
    void OnFindCitation(object control);

    [DispId(21)]
    void StyleChosen(object control, [MarshalAs(UnmanagedType.BStr)] string selectedId, int selectedIndex);
}

[ComImport]
[ComVisible(true)]
[Guid("000C0396-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IRibbonExtensibility
{
    [DispId(1)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetCustomUI([MarshalAs(UnmanagedType.BStr)] string ribbonId);
}
