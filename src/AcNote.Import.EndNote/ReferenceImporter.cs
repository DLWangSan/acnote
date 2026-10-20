using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcNote.Core;

namespace AcNote.Import.EndNote;

public static class ReferenceImporter
{
    public static readonly string[] Extensions = [".enl", ".enlx", ".xml", ".bib", ".bibtex", ".ris", ".json", ".sqlite"];
    public static bool Supports(string path) => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static ImportResult Import(string path)
    {
        var result = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".enl" or ".enlx" or ".xml" => EndNoteImporter.Import(path),
            ".bib" or ".bibtex" => BibTexReader.Read(ReadText(path)),
            ".ris" => ReadRis(ReadText(path)),
            ".json" => ReadJson(ReadText(path)),
            ".sqlite" => ZoteroReader.Read(path),
            _ => throw new InvalidDataException("不支持此文件。请选择 EndNote、BibTeX、RIS、CSL JSON 或 Zotero SQLite 文献库。"),
        };
        if (result.Items.Count == 0 && result.Warnings.Count == 0)
            throw new InvalidDataException("文件中没有可识别的文献记录。请确认选择的是文献库或文献导出文件。");
        // Record IDs are only unique inside their source library, never globally.
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())))[..20];
        var seen = new HashSet<string>();
        var index = 0;
        foreach (var item in result.Items)
        {
            index++;
            var key = item.SourceKey;
            if (string.IsNullOrWhiteSpace(key))
                key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item))));
            if (!seen.Add(key)) key += ":row:" + index;
            item.SourceKey = "import:" + scope + ":" + key;
            item.Doi = Regex.Replace(item.Doi.Trim(), @"^(?:https?://(?:dx\.)?doi\.org/|doi:\s*)", "", RegexOptions.IgnoreCase);
            if (string.IsNullOrWhiteSpace(item.Url)) item.Url = item.Extra.GetValueOrDefault("url") ?? "";
        }
        return result;
    }

    internal static string Year(string text) => Regex.Match(text, @"\d{4}").Value;
    private static string ReadText(string path)
    {
        try
        {
            using var reader = new StreamReader(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            // Legacy Chinese reference exports commonly use GBK / GB18030.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return File.ReadAllText(path, Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
        }
    }
    internal static string TypeOf(string type) => type.ToLowerInvariant() switch
    {
        "article" or "jour" or "journalarticle" or "article-journal" => "article-journal",
        "book" or "booklet" or "editedbook" => "book",
        "inbook" or "incollection" or "chap" or "booksection" or "chapter" => "chapter",
        "inproceedings" or "conference" or "conf" or "cpaper" or "conferencepaper" or "paper-conference" => "paper-conference",
        "preprint" or "article-preprint" => "preprint",
        "phdthesis" or "mastersthesis" or "thes" or "thesis" => "thesis",
        "dataset" or "data" => "dataset",
        _ => "document",
    };

    private static ImportResult ReadRis(string text)
    {
        var result = new ImportResult();
        Dictionary<string, List<string>>? fields = null;
        string? last = null;
        void Finish()
        {
            if (fields is null) return;
            string Get(params string[] names) => names.Select(n => fields.GetValueOrDefault(n)?.FirstOrDefault()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
            var title = Get("TI", "T1", "CT");
            if (title.Length == 0) result.Warnings.Add("RIS 记录没有题名，已跳过。");
            else
            {
                var start = Get("SP"); var end = Get("EP");
                result.Items.Add(new ReferenceItem
                {
                    Type = TypeOf(Get("TY")), Title = title, ContainerTitle = Get("JO", "JF", "T2", "JA"),
                    Year = Year(Get("PY", "Y1", "DA")), Volume = Get("VL"), Issue = Get("IS"),
                    Page = start + (end.Length > 0 && end != start ? "-" + end : ""),
                    Doi = Get("DO"), Url = Get("UR"), Publisher = Get("PB"), PublisherPlace = Get("CY", "PP"),
                    Language = Get("LA"), SourceKey = Get("ID"),
                    Authors = (fields.GetValueOrDefault("AU") ?? fields.GetValueOrDefault("A1") ?? []).Select(AuthorFormatter.Parse).ToList(),
                });
            }
            fields = null;
        }
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^([A-Z0-9]{2})\s{2}-\s?(.*)\r?$");
            if (match.Success)
            {
                var tag = match.Groups[1].Value;
                if (tag == "TY") { Finish(); fields = new(); }
                if (tag == "ER") { Finish(); last = null; continue; }
                if (fields is null) continue;
                if (!fields.ContainsKey(tag)) fields[tag] = [];
                fields[tag].Add(match.Groups[2].Value.Trim()); last = tag;
            }
            else if (fields is not null && last is not null && !string.IsNullOrWhiteSpace(line))
                fields[last][^1] += " " + line.Trim();
        }
        Finish();
        return result;
    }

    private static ImportResult ReadJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("请选择 Zotero 导出的 CSL JSON 文件（文献数组）。");
        var result = new ImportResult();
        foreach (var row in root.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) { result.Warnings.Add("跳过无效 JSON 记录。"); continue; }
            string Get(string name) => row.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : "";
            if (string.IsNullOrWhiteSpace(Get("title"))) { result.Warnings.Add("JSON 记录没有题名，已跳过。"); continue; }
            var item = new ReferenceItem { Title = Get("title"), Type = TypeOf(Get("type")), SourceKey = Get("id"),
                ContainerTitle = Get("container-title", "archive"), Volume = Get("volume"), Issue = Get("issue"), Page = Get("page"),
                Doi = Get("DOI"), Url = Get("URL"), Publisher = Get("publisher"), PublisherPlace = Get("publisher-place"), Language = Get("language") };
            if (row.TryGetProperty("issued", out var issued) && issued.ValueKind == JsonValueKind.Object)
            {
                if (issued.TryGetProperty("date-parts", out var parts) && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0 && parts[0].ValueKind == JsonValueKind.Array && parts[0].GetArrayLength() > 0)
                    item.Year = parts[0][0].ToString();
                else if (issued.TryGetProperty("raw", out var raw)) item.Year = Year(raw.ToString());
            }
            if (row.TryGetProperty("author", out var authors) && authors.ValueKind == JsonValueKind.Array)
                foreach (var author in authors.EnumerateArray())
                {
                    if (author.ValueKind != JsonValueKind.Object) continue;
                    string Name(string n) => author.TryGetProperty(n, out var v) ? v.ToString() : "";
                    item.Authors.Add(new Author { Family = Name("family"), Given = Name("given"), Literal = Name("literal") });
                }
            foreach (var name in new[] { "edition", "ISBN", "ISSN", "abstract" })
                if (Get(name).Length > 0) item.Extra[name] = Get(name);
            var archiveId = Get("archive_location", "archive-id");
            if (archiveId.Length > 0) item.Extra["archive-id"] = archiveId;
            var archive = Get("archive");
            if (archive.Length > 0) item.Extra["archive"] = archive;
            result.Items.Add(item);
        }
        return result;
    }
}
