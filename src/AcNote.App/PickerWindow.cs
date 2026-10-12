using System.Windows;
using System.Windows.Controls;
using AcNote.Core;

namespace AcNote.App;

public sealed class PickerWindow : Window
{
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Extended };
    private readonly RadioButton _narrative;

    public PickerWindow(Session session, string form = "parenthetical", IReadOnlyList<string>? selectedIds = null)
    {
        Title = "AcNote 插入引用";
        Width = 680;
        Height = 540;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(247, 249, 252));
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
        FontSize = 14;

        var root = new DockPanel { Margin = new Thickness(16) };
        var hint = new TextBlock
        {
            Text = "按住 Ctrl 可以在同一处插入多篇。顺序编码会写成 [1,2] 或 [3-5]；著者-出版年可以写成（作者，年），也可以写成作者（年）。换版式以后都会更新。",
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
        var insert = new Button { Content = "插入", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "取消", MinWidth = 88, IsCancel = true };
        insert.Click += (_, _) =>
        {
            SelectedIds = _list.SelectedItems.OfType<PickRow>().Select(row => row.Id).ToList();
            if (SelectedIds.Count == 0)
            {
                MessageBox.Show(this, "请先选择文献。", "AcNote");
                return;
            }
            DialogResult = true;
        };
        buttons.Children.Add(insert);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var forms = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
        };
        forms.Children.Add(new RadioButton
        {
            Content = "（作者，年）",
            GroupName = "cite-form",
            IsChecked = form != "narrative",
            Margin = new Thickness(0, 0, 16, 0),
        });
        _narrative = new RadioButton
        {
            Content = "作者（年）",
            GroupName = "cite-form",
            IsChecked = form == "narrative",
        };
        forms.Children.Add(_narrative);
        DockPanel.SetDock(forms, Dock.Bottom);

        var style = session.CurrentStyle;
        foreach (var item in session.Store?.List() ?? [])
        {
            var missing = MissingFieldChecker.Check(item, style);
            _list.Items.Add(new PickRow(
                item.Id,
                string.IsNullOrWhiteSpace(item.Title) ? "未命名" : item.Title,
                $"{AuthorFormatter.FormatList(item, style.EtAl)} · {item.Year} · {ReferenceTypes.LabelOf(item.Type)}".Trim(),
                missing.Count == 0 ? "" : "缺" + string.Join("、", missing)));
        }

        foreach (PickRow row in _list.Items)
            if (selectedIds?.Contains(row.Id) == true) _list.SelectedItems.Add(row);
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 12), ToolTip = "搜索题名、作者或年份" };
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_list.Items);
        search.TextChanged += (_, _) => view.Filter = value => value is PickRow row &&
            (row.Title + " " + row.Meta).Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase);
        DockPanel.SetDock(search, Dock.Top);
        _list.ItemTemplate = CreateTemplate();
        root.Children.Add(hint);
        root.Children.Add(search);
        root.Children.Add(buttons);
        root.Children.Add(forms);
        root.Children.Add(_list);
        Content = root;
    }

    public List<string> SelectedIds { get; private set; } = [];

    public string SelectedForm => _narrative.IsChecked == true ? "narrative" : "parenthetical";

    private static DataTemplate CreateTemplate()
    {
        var template = new DataTemplate(typeof(PickRow));
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.MarginProperty, new Thickness(2, 4, 2, 4));
        panel.AppendChild(Text(nameof(PickRow.Title), true));
        panel.AppendChild(Text(nameof(PickRow.Meta), false));
        var missing = Text(nameof(PickRow.Missing), false);
        missing.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Firebrick);
        panel.AppendChild(missing);
        template.VisualTree = panel;
        return template;
    }

    private static FrameworkElementFactory Text(string path, bool bold)
    {
        var block = new FrameworkElementFactory(typeof(TextBlock));
        block.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(path));
        if (bold)
            block.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        return block;
    }

    private sealed record PickRow(string Id, string Title, string Meta, string Missing);
}
