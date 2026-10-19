using System.Text;
using System.Text.RegularExpressions;

namespace AcNote.Core;

public sealed class FormattedReference
{
    public required string Text { get; init; }
    public required IReadOnlyList<string> Missing { get; init; }
    public IReadOnlyList<FormattedTextSpan> Spans { get; init; } = [];
}

public sealed record FormattedTextSpan(int Start, int Length, bool Italic, bool Bold);

public static class CitationNumbers
{
    public static string Format(IEnumerable<int> numbers, string rangeSeparator = "-")
    {
        var ordered = numbers.Where(number => number > 0).Distinct().OrderBy(number => number).ToList();
        if (ordered.Count == 0)
            return "";

        var parts = new List<string>();
        var index = 0;
        while (index < ordered.Count)
        {
            var start = ordered[index];
            var end = start;
            while (index + 1 < ordered.Count && ordered[index + 1] == end + 1)
            {
                index++;
                end = ordered[index];
            }

            if (end >= start + 2)
                parts.Add($"{start}{rangeSeparator}{end}");
            else if (end == start)
                parts.Add(start.ToString());
            else
            {
                parts.Add(start.ToString());
                parts.Add(end.ToString());
            }
            index++;
        }

        return "[" + string.Join(",", parts) + "]";
    }
}

public static class BibliographyFormatter
{
    public static FormattedReference Format(ReferenceItem item, ResolvedStyle style, string? yearLabel = null)
    {
        var tokens = style.Templates.TryGetValue(item.Type, out var typed)
            ? typed
            : style.Templates.GetValueOrDefault("document") ?? [];

        var builder = new StringBuilder();
        var spans = new List<FormattedTextSpan>();
        foreach (var token in tokens)
        {
            if (!string.IsNullOrEmpty(token.Literal) && string.IsNullOrEmpty(token.Field))
            {
                builder.Append(token.Literal);
                continue;
            }

            if (string.IsNullOrEmpty(token.Field))
                continue;

            var value = token.Field == "year" && yearLabel is not null ? yearLabel : FieldValue(item, token.Field, style);
            if (string.IsNullOrEmpty(value))
                continue;

            builder.Append(token.Prefix);
            var valueStart = builder.Length;
            builder.Append(value);
            builder.Append(token.Suffix);
            if (token.Italic == true || token.Bold == true)
                spans.Add(new(valueStart, value.Length, token.Italic == true, token.Bold == true));
        }

        var sourceText = builder.ToString();
        var text = Tidy(sourceText);
        var normalizedSpans = new List<FormattedTextSpan>();
        var searchFrom = 0;
        foreach (var span in spans)
        {
            var value = sourceText.Substring(span.Start, span.Length);
            var actualStart = text.IndexOf(value, searchFrom, StringComparison.Ordinal);
            if (actualStart < 0) continue;
            normalizedSpans.Add(span with { Start = actualStart });
            searchFrom = actualStart + value.Length;
        }

        return new FormattedReference
        {
            Text = text,
            Missing = MissingFieldChecker.Check(item, style),
            Spans = normalizedSpans,
        };
    }

    public static string FieldValue(ReferenceItem item, string field, ResolvedStyle style)
    {
        var value = field switch
        {
            "author" => AuthorFormatter.FormatList(item, style.EtAl),
            "title" => item.Title,
            "container-title" => item.ContainerTitle,
            "year" => item.Year,
            "volume" => item.Volume,
            "issue" => item.Issue,
            "page" => item.Page,
            "publisher" => item.Publisher,
            "publisher-place" => item.PublisherPlace,
            "doi" => CleanDoi(item.Doi),
            "doi-url" => string.IsNullOrWhiteSpace(item.Doi) ? "" : "https://doi.org/" + CleanDoi(item.Doi),
            "url" => item.Url,
            _ => item.Extra.GetValueOrDefault(field) ?? "",
        };
        return value?.Trim() ?? "";
    }

    private static string CleanDoi(string value) => Regex.Replace(value.Trim(), @"^(?:https?://(?:dx\.)?doi\.org/|doi:\s*)", "", RegexOptions.IgnoreCase);

    public static string Tidy(string text)
    {
        var current = text.Replace("\r", " ").Replace("\n", " ");
        for (var pass = 0; pass < 6; pass++)
        {
            var next = current;
            next = Regex.Replace(next, @"[ \t]{2,}", " ");
            next = Regex.Replace(next, @"\(\s*\)", "");
            next = Regex.Replace(next, @":\s*,", ",");
            next = Regex.Replace(next, @"\.\s*,", ".");
            next = Regex.Replace(next, @",\s*,", ",");
            next = Regex.Replace(next, @",\s*\.", ".");
            next = Regex.Replace(next, @"\.\s*\.", ".");
            next = Regex.Replace(next, @"\s+([,.:;)\]])", "$1");
            next = Regex.Replace(next, @"([(\[])\s+", "$1");
            next = Regex.Replace(next, @"\s{2,}", " ");
            next = next.Trim();
            if (next == current)
                break;
            current = next;
        }

        return current;
    }
}
