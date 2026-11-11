using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

internal sealed class AuthorSeparatorEditor : StackPanel
{
    private sealed record Choice(string Label, string? Value) { public override string ToString() => Label; }
    private readonly ComboBox _zh;
    private readonly ComboBox _en;
    public event Action? Changed;
    public string? ValueZh => (_zh.SelectedItem as Choice)?.Value;
    public string? ValueEn => (_en.SelectedItem as Choice)?.Value;

    public AuthorSeparatorEditor(string defaults)
    {
        Margin = new Thickness(0, 0, 0, 10);
        _zh = Add("中文作者之间", defaults);
        _en = Add("英文作者之间", defaults);
    }

    private ComboBox Add(string label, string defaults)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var choices = new ComboBox { MinWidth = 230, ToolTip = defaults };
        choices.Items.Add(new Choice("沿用版式原设置", null));
        foreach (var choice in new Choice[] {
            new("全角逗号（，）", "，"), new("半角逗号和空格（, ）", ", "),
            new("半角逗号（,）", ","), new("顿号（、）", "、"),
            new("半角分号和空格（; ）", "; "), new("全角分号（；）", "；"),
            new("空格", " "), new("无分隔", "") }) choices.Items.Add(choice);
        choices.SelectedIndex = 0;
        choices.SelectionChanged += (_, _) => Changed?.Invoke();
        row.Children.Add(choices); Children.Add(row);
        return choices;
    }

    public void Load(string? zh, string? en, string? legacy)
    {
        LoadChoice(_zh, zh ?? legacy);
        LoadChoice(_en, en ?? legacy);
    }

    private static void LoadChoice(ComboBox choices, string? value)
    {
        var choice = choices.Items.Cast<Choice>().FirstOrDefault(c => c.Value == value);
        if (choice is null) { choice = new Choice($"自定义：{value}", value); choices.Items.Add(choice); }
        choices.SelectedItem = choice;
    }
}
