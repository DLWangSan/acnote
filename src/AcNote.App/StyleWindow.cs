using System.Windows;
using System.Windows.Controls;
using AcNote.Core;

namespace AcNote.App;

public sealed class StyleWindow : Window
{
    private readonly ListBox _list = new();

    public StyleWindow(Session session)
    {
        Title = "AcNote 版式";
        Width = 520;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 241, 235));
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
        FontSize = 14;

        var root = new DockPanel { Margin = new Thickness(16) };
        var hint = new TextBlock
        {
            Text = "这份版式只作用于当前 Word 文档。文献库里的缺项检查会跟着文档版式走。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };
        DockPanel.SetDock(hint, Dock.Top);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var ok = new Button { Content = "使用", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "取消", MinWidth = 88, IsCancel = true };
        ok.Click += (_, _) =>
        {
            if (_list.SelectedItem is not ResolvedStyle style)
            {
                MessageBox.Show(this, "请选择一个版式。", "AcNote");
                return;
            }
            SelectedStyleId = style.Id;
            DialogResult = true;
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);

        _list.ItemsSource = session.Catalog.All;
        _list.DisplayMemberPath = nameof(ResolvedStyle.DisplayName);
        _list.SelectedItem = session.Catalog.All.FirstOrDefault(style => style.Id == session.StyleId);
        root.Children.Add(hint);
        root.Children.Add(buttons);
        root.Children.Add(_list);
        Content = root;
    }

    public string? SelectedStyleId { get; private set; }
}
