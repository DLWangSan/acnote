using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AcNote.WordAddin;

/// <summary>
/// Runs on its own STA so progress remains visible while Word's STA is busy
/// making synchronous document-model calls.
/// </summary>
internal sealed class RefreshProgressWindow : IDisposable
{
    private readonly ManualResetEventSlim _shown = new();
    private readonly Thread _thread;
    private Form? _form;
    private Label? _message;
    private ProgressBar? _progress;

    public RefreshProgressWindow()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "AcNote refresh progress" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_shown.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("无法显示更新进度窗口。");
    }

    private void Run()
    {
        var form = new Form
        {
            Text = "AcNote 正在更新",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(390, 100),
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            ShowInTaskbar = false,
            TopMost = true,
        };
        var message = new Label
        {
            AutoSize = false,
            Location = new Point(18, 15),
            Size = new Size(354, 24),
            Text = "正在准备更新…",
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var progress = new ProgressBar
        {
            Location = new Point(18, 52),
            Size = new Size(354, 22),
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous,
        };
        form.Controls.Add(message);
        form.Controls.Add(progress);
        form.Shown += (_, _) => _shown.Set();
        _form = form;
        _message = message;
        _progress = progress;
        Application.Run(form);
        _shown.Set();
    }

    public void Report(string message, int percent)
    {
        var form = _form;
        if (form is null || form.IsDisposed || !form.IsHandleCreated) return;
        try
        {
            form.BeginInvoke((Action)(() =>
            {
                if (form.IsDisposed) return;
                _message!.Text = message;
                _progress!.Value = Math.Clamp(percent, 0, 100);
            }));
        }
        catch (InvalidOperationException) { }
    }

    public void Dispose()
    {
        var form = _form;
        if (form is not null && !form.IsDisposed && form.IsHandleCreated)
        {
            try { form.BeginInvoke(form.Close); }
            catch (InvalidOperationException) { }
        }
        _thread.Join(TimeSpan.FromSeconds(2));
        _shown.Dispose();
    }
}
