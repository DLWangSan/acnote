using System.Text.Json;
using System.Text.Json.Serialization;

namespace AcNote.Core;

public sealed class StyleToken
{
    public string? Field { get; set; }
    public string? Literal { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}

public sealed class EtAlRule
{
    public int MinCount { get; set; }
    public int UseFirst { get; set; }
    public string? Zh { get; set; }
    public string? En { get; set; }

    public static EtAlRule Default() => new()
    {
        MinCount = 4,
        UseFirst = 3,
        Zh = ", 等",
        En = ", et al.",
    };
}

public sealed class StyleFile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Extends { get; set; }
    public string? Layout { get; set; }
    public string? Group { get; set; }
    public string? Status { get; set; }
    public string? Source { get; set; }

    [JsonIgnore]
    public string? FilePath { get; set; }

    public EtAlRule? EtAl { get; set; }
    public Dictionary<string, List<StyleToken>>? Templates { get; set; }
    public Dictionary<string, List<string>>? Required { get; set; }
}

public sealed class ResolvedStyle
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string Source { get; set; } = "";
    public string Layout { get; set; } = "numeric";
    public string Group { get; set; } = "";
    public string? FilePath { get; set; }
    public EtAlRule EtAl { get; set; } = EtAlRule.Default();
    public Dictionary<string, List<StyleToken>> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Required { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => Name;
}

public sealed class StyleCatalog
{
    public const string DefaultId = "gb-t-7714-2015-numeric";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, ResolvedStyle> _styles;

    private StyleCatalog(Dictionary<string, ResolvedStyle> styles)
    {
        _styles = styles;
    }

    public IReadOnlyList<ResolvedStyle> All =>
        _styles.Values
            .OrderBy(GroupRank)
            .ThenBy(style => style.Group == "classic" && style.Layout != "author-year" ? 0 : 1)
            .ThenBy(style => style.Name, StringComparer.CurrentCulture)
            .ToList();

    private static int GroupRank(ResolvedStyle style) => style.Group switch
    {
        "classic" => 0,
        "university" => 1,
        _ => 2,
    };

    public ResolvedStyle Get(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _styles.TryGetValue(id, out var style))
            return style;
        if (_styles.TryGetValue(DefaultId, out var fallback))
            return fallback;
        throw new InvalidOperationException("没有可用的版式。");
    }

    public static StyleCatalog Load(params string?[] directories)
    {
        var raw = new Dictionary<string, StyleFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                continue;

            foreach (var file in Directory.EnumerateFiles(directory, "*.style"))
            {
                StyleFile? style;
                try
                {
                    style = JsonSerializer.Deserialize<StyleFile>(File.ReadAllText(file), JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"版式文件无法读取：{Path.GetFileName(file)}。{ex.Message}");
                }

                if (string.IsNullOrWhiteSpace(style?.Id))
                    throw new InvalidOperationException($"版式文件缺少 id：{Path.GetFileName(file)}");

                style.FilePath = file;
                raw[style.Id] = style;
            }
        }

        var resolved = new Dictionary<string, ResolvedStyle>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in raw.Values)
            resolved[style.Id!] = Resolve(style, raw);

        return new StyleCatalog(resolved);
    }

    public static ResolvedStyle Resolve(StyleFile leaf, IReadOnlyDictionary<string, StyleFile> all)
    {
        var chain = new List<StyleFile>();
        var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = leaf;
        while (true)
        {
            if (string.IsNullOrWhiteSpace(current.Id) || !guard.Add(current.Id))
                throw new InvalidOperationException($"版式继承出现循环：{leaf.Id}");

            chain.Add(current);
            if (string.IsNullOrWhiteSpace(current.Extends))
                break;
            if (!all.TryGetValue(current.Extends, out var parent))
                throw new InvalidOperationException($"版式 {current.Id} 找不到父版式 {current.Extends}");
            current = parent;
        }

        chain.Reverse();
        var result = new ResolvedStyle
        {
            Id = leaf.Id ?? "",
            EtAl = EtAlRule.Default(),
        };

        foreach (var part in chain)
        {
            if (!string.IsNullOrWhiteSpace(part.Layout))
                result.Layout = part.Layout;
            if (!string.IsNullOrWhiteSpace(part.Group))
                result.Group = part.Group;
            if (!string.IsNullOrWhiteSpace(part.Source))
                result.Source = part.Source;
            if (part.EtAl is not null)
                ApplyEtAl(result.EtAl, part.EtAl);
            if (part.Templates is not null)
            {
                foreach (var (type, tokens) in part.Templates)
                    result.Templates[type] = tokens;
            }
            if (part.Required is not null)
            {
                foreach (var (type, fields) in part.Required)
                    result.Required[type] = fields;
            }
        }

        result.Name = string.IsNullOrWhiteSpace(leaf.Name) ? leaf.Id ?? "" : leaf.Name;
        result.Version = leaf.Version ?? "";
        result.Status = leaf.Status ?? "";
        result.Source = string.IsNullOrWhiteSpace(leaf.Source) ? result.Source : leaf.Source;
        result.Layout = string.IsNullOrWhiteSpace(leaf.Layout) ? result.Layout : leaf.Layout;
        result.Group = string.IsNullOrWhiteSpace(leaf.Group) ? result.Group : leaf.Group;
        result.FilePath = leaf.FilePath;
        if (string.IsNullOrWhiteSpace(result.Layout))
            result.Layout = "numeric";
        return result;
    }

    private static void ApplyEtAl(EtAlRule target, EtAlRule incoming)
    {
        if (incoming.MinCount > 0)
            target.MinCount = incoming.MinCount;
        if (incoming.UseFirst > 0)
            target.UseFirst = incoming.UseFirst;
        if (!string.IsNullOrEmpty(incoming.Zh))
            target.Zh = incoming.Zh;
        if (!string.IsNullOrEmpty(incoming.En))
            target.En = incoming.En;
    }
}
