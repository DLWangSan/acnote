using System.Windows;
using System.Windows.Controls;
using AcNote.Core;

namespace AcNote.App;

internal sealed class ExtraReferenceFields : StackPanel
{
    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> _boxes = new(StringComparer.OrdinalIgnoreCase);
    private string? _key;
    public event Action? Changed;
    private static readonly HashSet<string> MainFields = new(StringComparer.OrdinalIgnoreCase)
    { "author", "author-or-editor", "title", "container-title", "year", "volume", "issue", "page", "publisher", "publisher-place", "doi", "doi-url", "url" };

    public void Load(Dictionary<string, string> values)
    {
        _values = new(values, StringComparer.OrdinalIgnoreCase);
        _boxes.Clear(); Children.Clear(); _key = null;
    }

    public Dictionary<string, string> Read()
    {
        var result = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
        foreach (var (field, box) in _boxes) result[field] = box.Text.Trim();
        return result;
    }

    public void Configure(string type, ResolvedStyle style)
    {
        var fields = (style.Templates.GetValueOrDefault(type) ?? [])
            .Select(t => t.Field).OfType<string>().Concat(style.Required.GetValueOrDefault(type) ?? [])
            .Where(field => !MainFields.Contains(field)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // A proceedings volume may be credited to editors rather than personal authors.
        if (type == "proceedings") fields.Insert(0, "editors");
        var key = type + ":" + string.Join("|", fields);
        if (key == _key) return;
        _values = Read(); _boxes.Clear(); Children.Clear(); _key = key;
        if (fields.Count == 0) return;
        Children.Add(new TextBlock { Text = "补充著录信息", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 6) });
        var sample = new ReferenceItem { Type = type, Extra = _values };
        foreach (var field in fields)
        {
            Children.Add(new TextBlock { Text = MissingFieldChecker.Label(type, field), Foreground = System.Windows.Media.Brushes.DimGray });
            var box = new TextBox { Text = BibliographyFormatter.FieldValue(sample, field, style) };
            box.TextChanged += (_, _) => Changed?.Invoke();
            _boxes[field] = box; Children.Add(box);
        }
    }
}
