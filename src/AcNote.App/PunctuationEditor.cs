using AcNote.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

internal sealed class PunctuationEditor : StackPanel
{
    private sealed record Choice(string Label, string? Value) { public override string ToString() => Label; }
    private readonly ComboBox _authorYear, _narrative, _open, _close, _entries;

    public PunctuationEditor(CitationPunctuation punctuation)
    {
        Margin = new Thickness(20);
        Children.Add(new TextBlock { Text = "叙述式用于作者做主语：Smith (2020)；括号式用于句中或句末：(Smith, 2020)。\n选择“按语言默认”会根据中英文自动使用相应标点。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
        _authorYear = Add("括号式：作者与年份之间", punctuation.AuthorYearSeparator, [new("英文逗号和空格", ", "), new("英文逗号", ","), new("中文逗号", "，"), new("空格", " "), new("无分隔", "")]);
        _narrative = Add("叙述式：作者与年份括号之间", punctuation.NarrativeSeparator, [new("空格", " "), new("无空格", ""), new("逗号和空格", ", ")]);
        _open = Add("左括号", punctuation.OpenBracket, [new("半角 (", "("), new("全角 （", "（"), new("方括号 [", "["), new("无括号", "")]);
        _close = Add("右括号", punctuation.CloseBracket, [new("半角 )", ")"), new("全角 ）", "）"), new("方括号 ]", "]"), new("无括号", "")]);
        _entries = Add("多篇文献之间", punctuation.EntrySeparator, [new("英文分号和空格", "; "), new("中文分号", "；"), new("逗号和空格", ", ")]);
    }

    private ComboBox Add(string label, string? value, Choice[] choices)
    {
        Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4) });
        var box = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = false };
        box.Items.Add(new Choice("按语言默认", null));
        foreach (var choice in choices) box.Items.Add(choice);
        var selected = box.Items.Cast<Choice>().FirstOrDefault(c => c.Value == value);
        if (selected is null) { selected = new Choice($"自定义：{value}", value); box.Items.Add(selected); }
        box.SelectedItem = selected;
        Children.Add(box);
        return box;
    }

    public JsonNode? Read() => JsonSerializer.SerializeToNode(new CitationPunctuation
    {
        AuthorYearSeparator = ((Choice)_authorYear.SelectedItem).Value,
        NarrativeSeparator = ((Choice)_narrative.SelectedItem).Value,
        OpenBracket = ((Choice)_open.SelectedItem).Value,
        CloseBracket = ((Choice)_close.SelectedItem).Value,
        EntrySeparator = ((Choice)_entries.SelectedItem).Value
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
