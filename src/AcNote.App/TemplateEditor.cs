using AcNote.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

internal sealed class TemplateEditor : DockPanel
{
    private readonly Dictionary<string, List<StyleToken>> _templates;
    private readonly Dictionary<string, List<StyleToken>> _original;
    private readonly ResolvedStyle _style;
    private readonly ComboBox _type = new();
    private readonly ListBox _rows = new();
    private readonly TextBlock _preview = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
    private readonly CheckBox _all = new() { Content = "DOI / URL 应用于所有文献类型", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
    private static readonly (string Id, string Label)[] Fields =
    [("author", "作者"), ("title", "题名"), ("container-title", "期刊 / 书名"), ("year", "年份"), ("volume", "卷"), ("issue", "期"), ("page", "页码"), ("publisher", "出版社 / 学校"), ("publisher-place", "出版地"), ("doi", "DOI"), ("doi-url", "DOI 链接"), ("url", "URL"), ("literal", "固定文字")];

    public TemplateEditor(ResolvedStyle style)
    {
        _style = style;
        _templates = style.Templates.ToDictionary(p => p.Key, p => TemplateEditing.Copy(p.Value));
        _original = style.Templates.ToDictionary(p => p.Key, p => TemplateEditing.Copy(p.Value));
        Margin = new Thickness(12);
        var top = new StackPanel(); SetDock(top, Dock.Top); Children.Add(top);
        top.Children.Add(new TextBlock { Text = "选择类型后直接添加字段、上下移动。没有值的字段及其前后标点会自动省略。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        if (!string.IsNullOrWhiteSpace(style.Notes))
            top.Children.Add(new TextBlock { Text = style.Notes, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 0, 0, 8) });
        _type.ItemsSource = ReferenceTypes.All.Select(t => t.Id).Concat(_templates.Keys).Distinct().ToList();
        _type.ItemTemplate = null;
        var types = new ComboBox { ItemsSource = ReferenceTypes.All.Select(t => new TypeChoice(t.Id, t.Label)).Concat(_templates.Keys.Where(k => !ReferenceTypes.All.Any(t => t.Id == k)).Select(k => new TypeChoice(k, k))).ToList(), MinWidth = 170, HorizontalAlignment = HorizontalAlignment.Left };
        types.SelectionChanged += (_, _) => { _type.SelectedItem = ((TypeChoice)types.SelectedItem).Id; Render(); };
        top.Children.Add(types);
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        actions.Children.Add(Button("＋ DOI", () => Add("doi")));
        actions.Children.Add(Button("＋ DOI 链接", () => Add("doi-url")));
        actions.Children.Add(Button("＋ URL", () => Add("url")));
        actions.Children.Add(_all);
        actions.Children.Add(Button("＋ 字段", () => { Current.Add(new() { Field = "title", Prefix = " " }); Render(); }));
        actions.Children.Add(Button("撤销此类型修改", () => { _templates[TypeId] = TemplateEditing.Copy(_original.GetValueOrDefault(TypeId) ?? []); Render(); }));
        top.Children.Add(actions);
        top.Children.Add(new TextBlock { Text = "字段（或固定文字）                 前面的标点             后面的标点                 顺序", Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(4) });
        var bottom = new Border { Background = System.Windows.Media.Brushes.WhiteSmoke, Child = _preview, MinHeight = 135 };
        SetDock(bottom, Dock.Bottom); Children.Add(bottom);
        Children.Add(_rows);
        types.SelectedIndex = 0;
    }

    private sealed record TypeChoice(string Id, string Label) { public override string ToString() => Label; }
    private string TypeId => (string?)_type.SelectedItem ?? "article-journal";
    private List<StyleToken> Current
    {
        get { if (!_templates.TryGetValue(TypeId, out var list)) _templates[TypeId] = list = TemplateEditing.Copy(_templates.GetValueOrDefault("document") ?? []); return list; }
    }
    private static Button Button(string label, Action action)
    { var button = new Button { Content = label, Margin = new Thickness(2), Padding = new Thickness(6, 4, 6, 4) }; button.Click += (_, _) => action(); return button; }
    private void Add(string field)
    {
        if (_all.IsChecked == true)
            foreach (var type in ReferenceTypes.All.Select(t => t.Id).Concat(_templates.Keys).Distinct().ToList())
            {
                if (!_templates.ContainsKey(type)) _templates[type] = TemplateEditing.Copy(_templates.GetValueOrDefault("document") ?? []);
                TemplateEditing.AddField(_templates[type], field, field == "doi-url");
            }
        else TemplateEditing.AddField(Current, field, field == "doi-url");
        Render();
    }
    private void Render()
    {
        _rows.Items.Clear();
        foreach (var token in Current.ToList())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var choices = Fields.Select(f => new TypeChoice(f.Id, f.Label)).ToList();
            var key = token.Field ?? "literal";
            if (!choices.Any(c => c.Id == key)) choices.Add(new(key, key));
            var field = new ComboBox { ItemsSource = choices, SelectedItem = choices.First(c => c.Id == key), Width = 150, Margin = new Thickness(2) };
            field.SelectionChanged += (_, _) => { var id = ((TypeChoice)field.SelectedItem).Id; token.Field = id == "literal" ? null : id; token.Literal = id == "literal" ? token.Literal ?? "." : null; Render(); };
            row.Children.Add(field);
            if (token.Field is null) row.Children.Add(Edit(token.Literal, value => token.Literal = value, 246));
            else { row.Children.Add(Edit(token.Prefix, value => token.Prefix = value, 120)); row.Children.Add(Edit(token.Suffix, value => token.Suffix = value, 120)); }
            row.Children.Add(Button("↑", () => Move(token, -1)));
            row.Children.Add(Button("↓", () => Move(token, 1)));
            row.Children.Add(Button("移除", () => { Current.Remove(token); Render(); }));
            _rows.Items.Add(row);
        }
        Preview();
    }
    private ComboBox Edit(string? value, Action<string> set, double width)
    {
        var box = new ComboBox { IsEditable = true, Text = value ?? "", Width = width, Margin = new Thickness(2), ToolTip = "选择常用标点，也可直接输入；空白表示不添加", ItemsSource = new[] { "", ". ", ", ", ": ", " ", "(", ")", "[J]. ", "[M]. ", "[D]. ", "//", " DOI: " } };
        box.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => { set(box.Text); Preview(); }));
        return box;
    }
    private void Move(StyleToken token, int delta)
    { var index = Current.IndexOf(token); var target = index + delta; if (target < 0 || target >= Current.Count) return; Current.RemoveAt(index); Current.Insert(target, token); Render(); }
    private void Preview()
    {
        var style = new ResolvedStyle { EtAl = _style.EtAl };
        style.Templates[TypeId] = Current;
        var sample = new ReferenceItem { Type = TypeId, Title = "示例研究", Authors = [AuthorFormatter.Parse("张三")], Language = "zh", Year = "2024", ContainerTitle = "示例学报", Volume = "12", Issue = "3", Page = "15-23", Publisher = "示例出版社", PublisherPlace = "北京", Doi = "10.1234/example", Url = "https://example.org/paper" };
        var full = BibliographyFormatter.Format(sample, style).Text;
        sample.Doi = "";
        _preview.Text = "实时预览（示例数据）\n" + full + "\n\n没有 DOI 时\n" + BibliographyFormatter.Format(sample, style).Text;
    }
    public JsonNode? Read() => JsonSerializer.SerializeToNode(_templates, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public void Load(string json)
    {
        var node = JsonNode.Parse(json)?["templates"];
        if (node is null) return;
        var templates = node.Deserialize<Dictionary<string, List<StyleToken>>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (templates is null) return;
        foreach (var pair in templates) _templates[pair.Key] = TemplateEditing.Copy(pair.Value);
        Render();
    }
}
