using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AcNote.Core;

namespace AcNote.App;

internal sealed class UpdateWindow : Window
{
    private readonly TextBox _source = new();
    private readonly TextBox _notes = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
    private readonly Button _check = new() { Content = "检查更新" };
    private readonly Button _download = new() { Content = "下载安装包", IsEnabled = false };
    private readonly CancellationTokenSource _cancel = new();
    private string? _url;
    private static string SettingsPath => Path.Combine(AppPaths.Root, "updates.json");
    private static string Current => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0";
    public UpdateWindow()
    {
        Title = "检查更新"; Width = 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "AcNote " + Current, FontSize = 22 });
        panel.Children.Add(_status); panel.Children.Add(_notes);
        panel.Children.Add(new TextBlock { Text = "更新源（可填写国内镜像的 HTTPS 更新地址）", Margin = new Thickness(0, 16, 0, 4) });
        _source.Text = LoadSource(); panel.Children.Add(_source);
        var buttons = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        var reset = new Button { Content = "恢复 GitHub" };
        reset.Click += (_, _) => _source.Text = UpdateService.DefaultSource;
        _check.Click += async (_, _) => await Check();
        _download.Click += (_, _) => { if (_url is not null) OpenLink(_url); };
        var releases = new Button { Content = "版本发布页" }; releases.Click += (_, _) => OpenLink(UpdateService.ReleasesPage);
        foreach (var button in new[] { _check, _download, reset, releases }) buttons.Children.Add(button);
        panel.Children.Add(buttons); Content = panel;
        Loaded += async (_, _) => await Check(); Closed += (_, _) => _cancel.Cancel();
    }
    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(UpdateService.HttpsUrl(url)) { UseShellExecute = true }); }
        catch (Exception ex) { _status.Text = "无法打开浏览器：" + ex.Message; }
    }
    private static string LoadSource()
    {
        foreach (var path in new[] { SettingsPath, Path.Combine(AppContext.BaseDirectory, "updates.json") })
        {
            if (!File.Exists(path)) continue;
            try { using var json = JsonDocument.Parse(File.ReadAllText(path)); return UpdateService.HttpsUrl(json.RootElement.GetProperty("source").GetString() ?? ""); }
            catch { /* Fall back to the bundled source or GitHub. */ }
        }
        return UpdateService.DefaultSource;
    }
    private async Task Check()
    {
        _check.IsEnabled = false; _download.IsEnabled = false; _source.IsEnabled = false; _url = null;
        _status.Text = "正在检查更新…"; _notes.Clear();
        try
        {
            var source = UpdateService.HttpsUrl(_source.Text.Trim());
            var release = await UpdateService.CheckAsync(source, _cancel.Token);
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { source }));
            if (release is null) { _status.Text = "尚无已发布的正式安装包。"; return; }
            _notes.Text = release.Notes;
            if (release.Version <= UpdateService.ParseVersion(Current)) { _status.Text = "当前已是最新版本（" + Current + "）。"; return; }
            _status.Text = "发现新版本 " + release.Version.ToString(3) + "，下载后运行安装包即可升级。";
            _url = release.DownloadUrl; _download.IsEnabled = true;
        }
        catch (OperationCanceledException) { _status.Text = "检查超时或已取消，请重试或切换更新源。"; }
        catch (Exception ex) { _status.Text = "未能检查更新，请确认网络或切换更新源。\n" + ex.Message; }
        finally { _check.IsEnabled = true; _source.IsEnabled = true; }
    }
}
