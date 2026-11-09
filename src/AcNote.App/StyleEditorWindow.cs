using AcNote.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace AcNote.App;

public sealed class StyleEditorWindow : Window
{
    private readonly TextBox _editor;
    private readonly TextBox _bibMin;
    private readonly TextBox _bibFirst;
    private readonly TextBox _bibZh;
    private readonly TextBox _bibEn;
    private readonly TextBox _citeMin;
    private readonly TextBox _citeFirst;
    private readonly TextBox _citeZh;
    private readonly TextBox _citeEn;
    private readonly AuthorSeparatorEditor _bibSeparator = new("默认：半角逗号和空格");
    private readonly AuthorSeparatorEditor _citeSeparator = new("按语言默认（和 / and）");
    private bool _syncing;
    private readonly ResolvedStyle _resolved;
    private readonly LayoutEditor _citationLayout;
    private readonly LayoutEditor _bibliographyLayout;
    private readonly PunctuationEditor _punctuation;
    private readonly TemplateEditor _templates;

    public StyleEditorWindow(string json, ResolvedStyle resolved)
    {
        _resolved = resolved;
        Title = "版式 · 作者、字体与段落";
        _citationLayout = new LayoutEditor(resolved.CitationFormat, false);
        _bibliographyLayout = new LayoutEditor(resolved.BibliographyFormat, true);
        _punctuation = new PunctuationEditor(resolved.CitationPunctuation);
        _templates = new TemplateEditor(resolved);
        Width = 820;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(247, 249, 252));
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
        FontSize = 14;

        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var save = new Button { Content = "保存", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "取消", MinWidth = 88, IsCancel = true };
        save.Click += (_, _) => SaveClicked();
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var rules = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        rules.Children.Add(Heading("参考文献里的作者"));
        (_bibMin, _bibFirst, _bibZh, _bibEn) = AddRule(rules);
        rules.Children.Add(_bibSeparator);
        rules.Children.Add(Heading("文中引用里的作者"));
        (_citeMin, _citeFirst, _citeZh, _citeEn) = AddRule(rules);
        rules.Children.Add(_citeSeparator);
        DockPanel.SetDock(rules, Dock.Top);

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
        _editor.LostFocus += (_, _) =>
        {
            ReadRules(_editor.Text);
            try { _templates.Load(_editor.Text); } catch (JsonException) { /* Save reports malformed JSON. */ }
        };
        foreach (var box in new[] { _bibMin, _bibFirst, _bibZh, _bibEn, _citeMin, _citeFirst, _citeZh, _citeEn })
            box.TextChanged += (_, _) => WriteRulesIntoJson();

        _bibSeparator.Changed += WriteRulesIntoJson;
        _citeSeparator.Changed += WriteRulesIntoJson;
        ReadRules(json);
        _syncing = true;
        Fill(_bibMin, _bibFirst, _bibZh, _bibEn, JsonSerializer.SerializeToNode(resolved.EtAl, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) as JsonObject, 4, 3, ", 等", ", et al.");
        Fill(_citeMin, _citeFirst, _citeZh, _citeEn, JsonSerializer.SerializeToNode(resolved.CitationEtAl, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) as JsonObject, 3, 1, "等", " et al.");
        _bibSeparator.Load(resolved.EtAl.AuthorSeparator);
        _citeSeparator.Load(resolved.CitationEtAl.AuthorSeparator);
        _syncing = false;
        root.Children.Add(buttons);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "文献字段与预览", Content = _templates });
        var authorPanel = new DockPanel();
        var provenance = new TextBlock { Text = resolved.Source, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(provenance, Dock.Top); authorPanel.Children.Add(provenance);
        authorPanel.Children.Add(rules);
        authorPanel.Children.Add(_editor);
        tabs.Items.Add(new TabItem { Header = "作者与高级规则", Content = authorPanel });
        tabs.Items.Add(new TabItem { Header = "文中字体", Content = new ScrollViewer { Content = _citationLayout } });
        tabs.Items.Add(new TabItem { Header = "引文标点与空格", Content = new ScrollViewer { Content = _punctuation } });
        tabs.Items.Add(new TabItem { Header = "文末字体与段落", Content = new ScrollViewer { Content = _bibliographyLayout } });
        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, tabs) || tabs.SelectedIndex != 1) return;
            try
            {
                var node = JsonNode.Parse(_editor.Text)!.AsObject();
                node["templates"] = _templates.Read();
                _editor.Text = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException) { /* Preserve incomplete advanced edits. */ }
        };
        root.Children.Add(tabs);
        Content = root;
    }

    public string JsonText { get; private set; } = "";

    private void SaveClicked()
    {
        try
        {
            ValidateRule(_bibMin, _bibFirst);
            ValidateRule(_citeMin, _citeFirst);
            WriteRulesIntoJson();
            var node = JsonNode.Parse(_editor.Text)!.AsObject();
            node["bibliographyFormat"] = _bibliographyLayout.Read();
            node["templates"] = _templates.Read();
            node["status"] = "custom";
            var citation = node["citation"] as JsonObject ?? new JsonObject();
            citation["format"] = _citationLayout.Read();
            citation["punctuation"] = _punctuation.Read();
            node["citation"] = citation;
            JsonText = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "AcNote"); return; }
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
    }

    private void ReadRules(string json)
    {
        try
        {
            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null)
                return;
            _syncing = true;
            _bibSeparator.Load(ReadSeparator(node["etAl"], _resolved.EtAl.AuthorSeparator));
            _citeSeparator.Load(ReadSeparator(node["citation"]?["etAl"], _resolved.CitationEtAl.AuthorSeparator));
            Fill(_bibMin, _bibFirst, _bibZh, _bibEn, node["etAl"] as JsonObject, _resolved.EtAl.MinCount, _resolved.EtAl.UseFirst, _resolved.EtAl.Zh ?? "", _resolved.EtAl.En ?? "");
            Fill(_citeMin, _citeFirst, _citeZh, _citeEn, node["citation"]?["etAl"] as JsonObject, _resolved.CitationEtAl.MinCount, _resolved.CitationEtAl.UseFirst, _resolved.CitationEtAl.Zh ?? "", _resolved.CitationEtAl.En ?? "");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            /* Keep the boxes when the file is mid-edit. */
        }
        finally
        {
            _syncing = false;
        }
    }

    private void WriteRulesIntoJson()
    {
        if (_syncing)
            return;
        try
        {
            var node = JsonNode.Parse(_editor.Text) as JsonObject;
            if (node is null)
                return;
            node["etAl"] = RuleNode(_bibMin, _bibFirst, _bibZh, _bibEn, _bibSeparator.Value, node["etAl"] as JsonObject);
            var citation = node["citation"] as JsonObject ?? new JsonObject();
            citation["etAl"] = RuleNode(_citeMin, _citeFirst, _citeZh, _citeEn, _citeSeparator.Value, citation["etAl"] as JsonObject);
            node["citation"] = citation;
            _syncing = true;
            _editor.Text = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var previewRule = JsonSerializer.SerializeToNode(_resolved.EtAl)!.AsObject();
            previewRule["MinCount"] = int.Parse(_bibMin.Text);
            previewRule["UseFirst"] = int.Parse(_bibFirst.Text);
            previewRule["Zh"] = _bibZh.Text;
            previewRule["En"] = _bibEn.Text;
            previewRule["AuthorSeparator"] = _bibSeparator.Value;
            _templates.UpdateAuthors(previewRule.Deserialize<EtAlRule>()!);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            /* The raw file is saved only after it parses. */
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void Fill(TextBox min, TextBox first, TextBox zh, TextBox en, JsonObject? rule, int defaultMin, int defaultFirst, string defaultZh, string defaultEn)
    {
        min.Text = ReadInt(rule, "minCount", defaultMin);
        first.Text = ReadInt(rule, "useFirst", defaultFirst);
        zh.Text = rule?["zh"]?.GetValue<string>() ?? defaultZh;
        en.Text = rule?["en"]?.GetValue<string>() ?? defaultEn;
    }

    private static string? ReadSeparator(JsonNode? node, string? fallback) =>
        node is JsonObject rule && rule.TryGetPropertyValue("authorSeparator", out var value) ? value?.GetValue<string>() : fallback;

    private static string ReadInt(JsonObject? rule, string name, int fallback) =>
        rule?[name]?.GetValue<int>().ToString() ?? fallback.ToString();

    private static void ValidateRule(TextBox min, TextBox first)
    {
        if (!int.TryParse(min.Text, out var count) || !int.TryParse(first.Text, out var take) || count < 2 || take < 1 || take >= count)
            throw new InvalidOperationException("作者省略规则：起始人数至少为 2，保留人数至少为 1 且小于起始人数。");
    }

    private static JsonObject RuleNode(TextBox min, TextBox first, TextBox zh, TextBox en, string? separator, JsonObject? existing)
    {
        // Preserve advanced author formatting, including MDPI initials and separators.
        var rule = existing?.DeepClone().AsObject() ?? new JsonObject();
        rule["minCount"] = int.TryParse(min.Text.Trim(), out var minCount) ? minCount : 1;
        rule["useFirst"] = int.TryParse(first.Text.Trim(), out var useFirst) ? useFirst : 1;
        rule["zh"] = zh.Text;
        rule["en"] = en.Text;
        rule["authorSeparator"] = separator;
        return rule;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 6),
    };

    private static (TextBox Min, TextBox First, TextBox Zh, TextBox En) AddRule(Panel parent)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var min = Box("4", 42);
        var first = Box("3", 42);
        var zh = Box("等", 72);
        var en = Box(" et al.", 88);
        row.Children.Add(Label("达到"));
        row.Children.Add(min);
        row.Children.Add(Label("人时，列出前"));
        row.Children.Add(first);
        row.Children.Add(Label("人，中文加"));
        row.Children.Add(zh);
        row.Children.Add(Label("英文加"));
        row.Children.Add(en);
        parent.Children.Add(row);
        return (min, first, zh, en);
    }

    private static TextBox Box(string text, double width) => new()
    {
        Text = text,
        Width = width,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0),
    };
}
