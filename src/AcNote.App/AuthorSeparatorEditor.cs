using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

internal sealed class AuthorSeparatorEditor : StackPanel
{
    private sealed record Choice(string Label, string? Value) { public override string ToString() => Label; }
    private readonly ComboBox _choices = new() { MinWidth = 230 };
    public event Action? Changed;
    public string? Value => (_choices.SelectedItem as Choice)?.Value;

    public AuthorSeparatorEditor(string defaults)
    {
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 0, 0, 10);
        Children.Add(new TextBlock { Text = "作者之间", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        _choices.Items.Add(new Choice(defaults, null));
        foreach (var choice in new Choice[] {
            new("全角逗号（，）", "，"), new("半角逗号和空格（, ）", ", "),
            new("半角逗号（,）", ","), new("顿号（、）", "、"),
            new("半角分号和空格（; ）", "; "), new("全角分号（；）", "；"),
            new("空格", " "), new("无分隔", "") }) _choices.Items.Add(choice);
        _choices.SelectedIndex = 0;
        _choices.SelectionChanged += (_, _) => Changed?.Invoke();
        Children.Add(_choices);
    }

    public void Load(string? value)
    {
        var choice = _choices.Items.Cast<Choice>().FirstOrDefault(c => c.Value == value);
        if (choice is null) { choice = new Choice($"自定义：{value}", value); _choices.Items.Add(choice); }
        _choices.SelectedItem = choice;
    }
}
