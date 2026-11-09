using System.Windows;
using System.Windows.Controls;
using AcNote.Core;
using Microsoft.Win32;

namespace AcNote.App;

internal sealed class StyleManagementWindow : Window
{
    private readonly StyleLibrary _library;
    private readonly ListBox _list = new();
    private readonly Button _rename = new() { Content = "重命名" };
    private readonly Button _delete = new() { Content = "删除" };
    private sealed record Entry(ResolvedStyle Style, bool BuiltIn)
    {
        public override string ToString() => Style.DisplayName + (BuiltIn ? "  · 内置" : "  · 自定义");
    }
    private Entry? Selected => _list.SelectedItem as Entry;
    public string? SelectedStyleId => Selected?.Style.Id;

    public StyleManagementWindow(StyleLibrary library, string current)
    {
        _library = library;
        Title = "管理版式"; Width = 720; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"); FontSize = 14;
        var root = new DockPanel { Margin = new Thickness(20) };
        var hint = new TextBlock { Text = "内置版式只读，编辑后需另存为新名称。自定义版式支持编辑、重命名和删除。\n旧版覆盖内置的修改会保留为“旧版自定义”副本；继续使用时请选择该副本。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var buttons = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        void Add(string title, Action action)
        {
            var button = new Button { Content = title };
            button.Click += (_, _) => Run(action); buttons.Children.Add(button);
        }
        Add("新增副本", () => EditSelected(true));
        Add("编辑 / 另存为", () => EditSelected(false));
        _rename.Click += (_, _) => Run(() =>
        {
            if (Selected is not Entry entry || entry.BuiltIn) return;
            var name = AskName(this, "重命名版式", entry.Style.Name);
            if (name is null) return;
            _library.Rename(entry.Style, name); Reload(entry.Style.Id);
        });
        buttons.Children.Add(_rename);
        _delete.Click += (_, _) => Run(() =>
        {
            if (Selected is not Entry entry || entry.BuiltIn) return;
            if (MessageBox.Show(this, $"删除自定义版式“{entry.Style.Name}”？\n已使用此版式的文档不会被修改；下次更新文档前需要重新选择可用版式。", "删除版式", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _library.Delete(entry.Style); Reload(StyleCatalog.DefaultId);
        });
        buttons.Children.Add(_delete);
        Add("导入 .style", () =>
        {
            var dialog = new OpenFileDialog { Title = "导入自定义版式", Filter = "AcNote 版式 (*.style)|*.style" };
            if (dialog.ShowDialog(this) == true) Reload(_library.Import(System.IO.File.ReadAllText(dialog.FileName)));
        });
        Add("关闭", Close);
        root.Children.Add(_list); Content = root;
        _list.SelectionChanged += (_, _) => { _rename.IsEnabled = _delete.IsEnabled = Selected is { BuiltIn: false }; };
        _list.MouseDoubleClick += (_, _) => Run(() => EditSelected(false));
        Reload(current);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "AcNote"); }
    }

    private void Reload(string? selected)
    {
        var items = _library.Load().All.Select(s => new Entry(s, _library.IsBuiltIn(s.Id))).ToList();
        _list.ItemsSource = items;
        _list.SelectedItem = items.FirstOrDefault(x => x.Style.Id == selected) ?? items.FirstOrDefault();
    }

    private void EditSelected(bool copy)
    {
        if (Selected is not Entry entry) return;
        var id = Edit(this, _library, entry.Style, copy);
        if (id is not null) Reload(id);
    }

    public static string? Edit(Window owner, StyleLibrary library, ResolvedStyle style, bool copy = false)
    {
        var mustCopy = copy || library.IsBuiltIn(style.Id);
        var editor = new StyleEditorWindow(StyleLibrary.Snapshot(style).ToJsonString(), style) { Owner = owner };
        if (mustCopy) editor.Title = "编辑版式 · 另存为自定义版式";
        if (editor.ShowDialog() != true) return null;
        if (mustCopy)
        {
            var suggested = style.Name + "（自定义）";
            while (true)
            {
                var name = AskName(owner, "另存为自定义版式", suggested);
                if (name is null) return null;
                try { return library.CreateCopy(style, name, editor.JsonText); }
                catch (InvalidOperationException ex) { MessageBox.Show(owner, ex.Message, "AcNote"); suggested = name; }
            }
        }
        library.Save(style, editor.JsonText);
        return style.Id;
    }

    private static string? AskName(Window owner, string title, string initial)
    {
        var dialog = new Window { Owner = owner, Title = title, Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "版式名称", Margin = new Thickness(0, 0, 0, 8) });
        var name = new TextBox { Text = initial }; panel.Children.Add(name);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var save = new Button { Content = "确定", IsDefault = true };
        save.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(name.Text)) dialog.DialogResult = true; };
        buttons.Children.Add(save); buttons.Children.Add(new Button { Content = "取消", IsCancel = true });
        panel.Children.Add(buttons); dialog.Content = panel;
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
        return dialog.ShowDialog() == true ? name.Text.Trim() : null;
    }
}
