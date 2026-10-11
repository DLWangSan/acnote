using System.Windows;
using AcNote.Core;

namespace AcNote.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private ControlServer? _server;
    private bool _ownsMutex;

    private void OnStartup(object sender, StartupEventArgs e)
    {
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
            ControlPipe.Transact("{\"cmd\":\"activate\"}", attempts: 4, delayMs: 200);
            Shutdown();
            return;
        }

        _ownsMutex = true;

        try
        {
            AppPaths.EnsureStyles(Path.Combine(AppContext.BaseDirectory, "styles"));
            var store = new LibraryStore(AppPaths.Database);
            var catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"), AppPaths.Styles);
            if (store.IsNew)
                SampleLibrary.Seed(store);

            var session = new Session(store, catalog);
            var window = new MainWindow(session);
            MainWindow = window;
            window.Show();
            window.SetWordStatus(WordAddinRegistrar.TryRegister());
            _server = new ControlServer(session, window);
            _server.Start();
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
            try { _mutex?.ReleaseMutex(); } catch { /* 进程退出时互斥量可能已释放 */ }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
