using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

public sealed class StyleEditorWindow : Window
{
    private readonly TextBox _editor;

    public StyleEditorWindow(string json)
    {
        Title = "编辑版式";
        Width = 760;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 241, 235));
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
        FontSize = 14;

        var root = new DockPanel { Margin = new Thickness(16) };
        _editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            Text = json,
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var save = new Button { Content = "保存", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "取消", MinWidth = 88, IsCancel = true };
        save.Click += (_, _) =>
        {
            JsonText = _editor.Text;
            try
            {
                using var document = JsonDocument.Parse(JsonText);
                if (!document.RootElement.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
                {
                    MessageBox.Show(this, "版式文件需要 id。", "AcNote");
                    return;
                }
            }
            catch (JsonException ex)
            {
                MessageBox.Show(this, "这不是有效的 JSON。" + ex.Message, "AcNote");
                return;
            }

            DialogResult = true;
        };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);

        root.Children.Add(buttons);
        root.Children.Add(_editor);
        Content = root;
    }

    public string JsonText { get; private set; } = "";
}
