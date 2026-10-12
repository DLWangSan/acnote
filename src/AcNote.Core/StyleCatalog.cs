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

    public static EtAlRule CitationDefault() => new()
    {
        MinCount = 3,
        UseFirst = 1,
        Zh = "等",
        En = " et al.",
    };
}

public sealed class CitationStyle
{
    public TextLayout? Format { get; set; }
    public EtAlRule? EtAl { get; set; }
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
    public CitationStyle? Citation { get; set; }
    public TextLayout? BibliographyFormat { get; set; }
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
    public EtAlRule CitationEtAl { get; set; } = EtAlRule.CitationDefault();
    public Dictionary<string, List<StyleToken>> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Required { get; } = new(StringComparer.OrdinalIgnoreCase);

    public TextLayout CitationFormat { get; } = new();
    public TextLayout BibliographyFormat { get; } = new();
    public string DisplayName => Status == "unverified" ? Name + "（待校验）" : Name;
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

    public static string Install(string json, string destination, params string?[] baseDirectories)
    {
        var style = JsonSerializer.Deserialize<StyleFile>(json, JsonOptions)
            ?? throw new InvalidOperationException("版式文件为空。");
        var id = style.Id;
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("版式 id 不是有效的文件名。");
        var temp = Directory.CreateTempSubdirectory("acnote-style-check-");
        try
        {
            File.WriteAllText(Path.Combine(temp.FullName, id + ".style"), json);
            Load([.. baseDirectories, destination, temp.FullName]);
            Directory.CreateDirectory(destination);
            var target = Path.Combine(destination, id + ".style");
            var pending = target + ".pending";
            File.WriteAllText(pending, json);
            File.Move(pending, target, overwrite: true);
            return id;
        }
        finally { temp.Delete(true); }
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
            CitationEtAl = EtAlRule.CitationDefault(),
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
            if (part.Citation?.EtAl is not null)
                ApplyEtAl(result.CitationEtAl, part.Citation.EtAl);
            if (part.Citation?.Format is not null) result.CitationFormat.Merge(part.Citation.Format);
            if (part.BibliographyFormat is not null) result.BibliographyFormat.Merge(part.BibliographyFormat);
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
        result.CitationFormat.Validate();
        result.BibliographyFormat.Validate();
        return result;
    }

    private static void ApplyEtAl(EtAlRule target, EtAlRule incoming)
    {
        if (incoming.MinCount > 0)
            target.MinCount = incoming.MinCount;
        if (incoming.UseFirst > 0)
            target.UseFirst = incoming.UseFirst;
        if (incoming.Zh is not null)
            target.Zh = incoming.Zh;
        if (incoming.En is not null)
            target.En = incoming.En;
    }
}
