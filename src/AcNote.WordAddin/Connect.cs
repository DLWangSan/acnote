using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using System.Text;
using AcNote.Core;
using AcNote.Word;
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

    public void OnRibbonLoad(object ribbon) => _ribbon = ribbon;

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
