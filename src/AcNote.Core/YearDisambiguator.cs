namespace AcNote.Core;

/// <summary>Document-scoped year labels; never changes stored metadata.</summary>
public static class YearDisambiguator
{
    public static IReadOnlyDictionary<string, string> Resolve(IEnumerable<ReferenceItem> items, EtAlRule rule)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var unique = items.DistinctBy(item => item.Id).ToList();
        foreach (var group in unique.GroupBy(item => (InTextFormatter.AuthorLabel(item, rule), item.Year.Trim())))
        {
            if (group.Count() < 2 || string.IsNullOrWhiteSpace(group.Key.Item2)) continue;
            var sorted = group.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
            for (var i = 0; i < sorted.Count; i++) result[sorted[i].Id] = sorted[i].Year.Trim() + Suffix(i);
        }
        return result;
    }

    private static string Suffix(int index)
    {
        var value = "";
        do { value = (char)('a' + index % 26) + value; index = index / 26 - 1; } while (index >= 0);
        return value;
    }
}
