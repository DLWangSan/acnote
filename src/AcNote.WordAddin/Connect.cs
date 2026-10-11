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
        <customUI xmlns="http://schemas.microsoft.com/office/2009/07/customui">
          <ribbon>
            <tabs>
              <tab id="tabAcNote" label="AcNote">
                <group id="grpAcNote" label="参考文献">
                  <button id="btnInsert" label="插入引用" size="large" onAction="OnInsertCitation"/>
                  <button id="btnEdit" label="编辑引用" size="large" onAction="OnEditCitation"/>
                  <button id="btnUpdate" label="更新引用" size="large" onAction="OnUpdateCitations"/>
                  <button id="btnStyle" label="版式" size="large" onAction="OnChooseStyle"/>
                </group>
              </tab>
            </tabs>
          </ribbon>
        </customUI>
        """;

    private object? _application;

    public void OnConnection(
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object application,
        ext_ConnectMode connectMode,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "dispatch")] object addInInst,
        [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(VariantDump), MarshalCookie = "ignore")] object custom)
    {
        _application = application;
        Log(_application is null ? "connected no-app" : "connected app");
        try
        {
            AppPaths.EnsureStyles(Path.Combine(AppContext.BaseDirectory, "styles"));
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

    public void OnInsertCitation(object control) => Guard(() =>
    {
        var (document, selection) = Active();
        var picked = Ask("{\"cmd\":\"pick\"}");
        if (!picked.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return;
        var ids = picked.GetProperty("ids").EnumerateArray().Select(item => item.GetString() ?? "").Where(id => id.Length > 0).ToList();
        var style = ResolveStyle(document, picked);
        CitationDocument.InsertAndRefresh(document, selection, ids, FormOf(picked), style, Lookup);
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
        var picked = Ask("{\"cmd\":\"pick\",\"form\":\"" + current + "\"}");
        if (!picked.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return;
        var ids = picked.GetProperty("ids").EnumerateArray().Select(item => item.GetString() ?? "").Where(id => id.Length > 0).ToList();
        var style = ResolveStyle(document, picked);
        CitationDocument.ReplaceSelectedAndRefresh(document, selection, ids, FormOf(picked), style, Lookup);
    });

    public void OnUpdateCitations(object control) => Guard(() =>
    {
        var (document, _) = Active();
        CitationDocument.Refresh(document, ResolveStyle(document, null), Lookup);
    });

    public void OnChooseStyle(object control) => Guard(() =>
    {
        var (document, _) = Active();
        var picked = Ask("{\"cmd\":\"choose-style\"}");
        if (!picked.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return;
        var style = Catalog().Get(picked.GetProperty("styleId").GetString());
        CitationDocument.SetStyleId(document, style.Id);
        CitationDocument.Refresh(document, style, Lookup);
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

    private static StyleCatalog Catalog() =>
        StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"), AppPaths.Styles);

    private static ReferenceItem? Lookup(string id)
    {
        var path = WorkspaceSettings.LibraryPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("请先在 AcNote 里打开一个文献库。");
        return new LibraryStore(path).Find(id);
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
            /* 主日志失败时仍写临时文件 */
        }

        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "acnote-word.log"), line, Encoding.UTF8);
        }
        catch
        {
            /* 日志失败不影响引用 */
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
