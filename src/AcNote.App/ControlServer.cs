using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AcNote.Core;

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
            /* 没有正在等待的连接 */
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
                /* 下一轮再接受连接 */
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
                "activate" => OnUi(() =>
                {
                    _window.Activate();
                    _window.WindowState = System.Windows.WindowState.Normal;
                    return "{\"ok\":true}";
                }),
                "pick" => OnUi(() => Pick(form)),
                "choose-style" => OnUi(ChooseStyle),
                _ => Fail("未知命令"),
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private string Pick(string? form)
    {
        if (_session.Store is null)
            return Fail("请先在 AcNote 里打开一个文献库。");
        _window.SaveShown();
        var dialog = new PickerWindow(_session, form == "narrative" ? "narrative" : "parenthetical") { Owner = _window };
        if (dialog.ShowDialog() != true)
            return "{\"ok\":false,\"cancelled\":true}";
        return JsonSerializer.Serialize(new { ok = true, ids = dialog.SelectedIds, styleId = _session.StyleId, form = dialog.SelectedForm });
    }

    private string ChooseStyle()
    {
        var dialog = new StyleWindow(_session) { Owner = _window };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedStyleId))
            return "{\"ok\":false,\"cancelled\":true}";
        return JsonSerializer.Serialize(new { ok = true, styleId = dialog.SelectedStyleId });
    }

    private string OnUi(Func<string> action) => _window.Dispatcher.Invoke(action);

    private static string Fail(string message) => JsonSerializer.Serialize(new { ok = false, error = message });
}
