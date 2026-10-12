using System.Windows;
using System.Text.Json;
using AcNote.Core;

namespace AcNote.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private ControlServer? _server;
    private bool _ownsMutex;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try { FileAssociations.Register(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        // Allow updating registration without opening a second library window or
        // letting the single-instance redirect silently keep an old installation.
        if (e.Args.Contains("--register-word", StringComparer.OrdinalIgnoreCase))
        {
            var status = WordAddinRegistrar.TryRegister();
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(Path.Combine(AppPaths.Root, "word-registration.log"), status);
            Shutdown(status.Contains("已登记", StringComparison.Ordinal) ? 0 : 1);
            return;
        }

        var createdNew = false;
        try
        {
            _mutex = new Mutex(true, @"Local\AcNote.SingleInstance", out createdNew);
        }
        catch (AbandonedMutexException)
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            var paths = e.Args.Where(a => !a.StartsWith("--")).Select(Path.GetFullPath).ToArray();
            var reply = ControlPipe.Transact(paths.Length == 0 ? "{\"cmd\":\"activate\"}" : JsonSerializer.Serialize(new { cmd = "open-files", paths }), attempts: 20, delayMs: 200);
            if (reply is null) MessageBox.Show("无法连接已运行的 AcNote，请关闭旧窗口后重新打开这些文件。", "AcNote");
            else if (paths.Length > 0)
            {
                try
                {
                    using var response = JsonDocument.Parse(reply);
                    if (!response.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                        MessageBox.Show("当前运行的 AcNote 尚不支持文件导入入口，请退出旧版本后重新打开文件。", "AcNote");
                }
                catch (JsonException) { MessageBox.Show("AcNote 未能接收文件，请重启后重试。", "AcNote"); }
            }
            Shutdown();
            return;
        }

        _ownsMutex = true;

        try
        {
            AppPaths.EnsureStyles(Path.Combine(AppContext.BaseDirectory, "styles"));
            var catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"), AppPaths.Styles);
            LibraryStore? store = null;
            var libraryPath = WorkspaceSettings.LibraryPath;
            if (!string.IsNullOrWhiteSpace(libraryPath) && File.Exists(libraryPath))
                store = new LibraryStore(libraryPath);
            else
                libraryPath = null;

            var session = new Session(store, catalog, libraryPath);
            var window = new MainWindow(session);
            MainWindow = window;
            window.Show();
            window.SetWordStatus(WordAddinRegistrar.TryRegister());
            _server = new ControlServer(session, window);
            _server.Start();
            var incoming = e.Args.Where(a => !a.StartsWith("--")).Select(Path.GetFullPath).ToArray();
            if (incoming.Length > 0) window.OpenFiles(incoming);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _server?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { /* The mutex may already be released while the process exits. */ }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
