using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AcNote.Core;

namespace AcNote.App;

public sealed class LayoutEditor : StackPanel
{
    private readonly Dictionary<string, TextBox> _fields = [];
    private readonly CheckBox _superscript = new() { Content = "上标", IsThreeState = true };
    private readonly ComboBox _alignment = new() { ItemsSource = new[] { "继承", "左对齐", "两端对齐", "居中", "右对齐" } };
    private readonly bool _paragraph;
    public LayoutEditor(TextLayout format, bool paragraph)
    {
        _paragraph = paragraph;
        Margin = new Thickness(4, 12, 4, 12);
        Add("fontFamily", "西文字体（留空继承文档）", format.FontFamily);
        Add("eastAsiaFontFamily", "中文字体（留空继承文档）", format.EastAsiaFontFamily);
        Add("fontSize", "字号 / 磅（5–96）", format.FontSize);
        _superscript.IsChecked = format.Superscript;
        Children.Add(_superscript);
        if (!paragraph) return;
        Add("lineSpacing", "行距 / 倍数（0.5–5）", format.LineSpacing);
        Add("lineSpacingPoints", "固定行距 / 磅（填写后优先于倍数）", format.LineSpacingPoints);
        Add("spaceBefore", "段前 / 磅", format.SpaceBefore);
        Add("spaceAfter", "段后 / 磅", format.SpaceAfter);
        Add("hangingIndent", "悬挂缩进 / 磅", format.HangingIndent);
        Children.Add(new TextBlock { Text = "段落对齐", Margin = new Thickness(0, 8, 0, 4) });
        _alignment.SelectedIndex = format.Alignment switch { "left" => 1, "justify" => 2, "center" => 3, "right" => 4, _ => 0 };
        Children.Add(_alignment);
    }

    private void Add(string key, string label, object? value)
    {
        Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 4) });
        var box = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
        _fields[key] = box;
        Children.Add(box);
    }

    public JsonObject Read()
    {
        var node = new JsonObject();
        foreach (var (key, box) in _fields)
        {
            var text = box.Text.Trim();
            if (text.Length == 0) continue;
            if (key.EndsWith("Family")) node[key] = text;
            else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) node[key] = number;
            else throw new InvalidOperationException("字体段落设置中的数值无效：" + text);
        }
        if (_superscript.IsChecked is bool super) node["superscript"] = super;
        if (_paragraph && _alignment.SelectedIndex > 0)
            node["alignment"] = new[] { "", "left", "justify", "center", "right" }[_alignment.SelectedIndex];
        node.Deserialize<TextLayout>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Validate();
        return node;
    }
}
