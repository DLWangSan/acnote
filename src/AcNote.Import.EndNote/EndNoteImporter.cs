using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AcNote.Core;
using Microsoft.Data.Sqlite;

namespace AcNote.Import.EndNote;

public sealed class ImportResult
{
    public List<ReferenceItem> Items { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class EndNoteImporter
{
    private static readonly string[] Collations = ["ENCI_Base", "ENCIN_Base", "ENCI", "ENOTE"];

    private static readonly Dictionary<int, string> ReferenceTypeMap = new()
    {
        [0] = "article-journal",
        [1] = "book",
        [2] = "thesis",
        [5] = "chapter",
        [6] = "book",
        [10] = "paper-conference",
        [13] = "document",
        [17] = "article-journal",
        [32] = "thesis",
        [47] = "document",
    };

    private static readonly Dictionary<string, string> XmlTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Journal Article"] = "article-journal",
        ["Book"] = "book",
        ["Book Section"] = "chapter",
        ["Thesis"] = "thesis",
        ["Conference Paper"] = "paper-conference",
        ["Conference Proceedings"] = "paper-conference",
        ["Report"] = "document",
        ["Web Page"] = "document",
    };

    public static ImportResult Import(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("找不到要导入的文件。", path);

        if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return ReadXml(path);

        if (path.EndsWith(".enlx", StringComparison.OrdinalIgnoreCase))
            return ReadCompressed(path);

        return ReadLibrary(path);
    }

    private static ImportResult ReadCompressed(string path)
    {
        var temp = Directory.CreateTempSubdirectory("acnote-enlx-");
        try
        {
            ZipFile.ExtractToDirectory(path, temp.FullName);
            var enl = Directory.EnumerateFiles(temp.FullName, "*.enl", SearchOption.AllDirectories).FirstOrDefault();
            var sqlite = (enl is not null ? ResolveSqlite(enl) : null)
                ?? Directory.EnumerateFiles(temp.FullName, "*.eni", SearchOption.AllDirectories).FirstOrDefault(IsReferenceDatabase);
            if (sqlite is null)
                throw new InvalidDataException("压缩库里没有可读取的 EndNote 数据。请改用 EndNote XML 导出。");
            return ReadSqlite(sqlite);
        }
        finally
        {
            try { temp.Delete(true); } catch { /* Leave the temp directory for a later cleanup. */ }
        }
    }

    private static ImportResult ReadLibrary(string path)
    {
        var sqlite = ResolveSqlite(path);
        if (sqlite is null)
        {
            throw new InvalidDataException(
                "无法读取这个 EndNote 库。EndNote X9.3 及以后的库可以直接导入；更早的库请在 EndNote 中导出为 EndNote XML。");
        }

        return ReadSqlite(sqlite);
    }

    private static string? ResolveSqlite(string enlPath)
    {
        if (IsReferenceDatabase(enlPath))
            return enlPath;

        var directory = Path.GetDirectoryName(enlPath);
        if (directory is null)
            return null;

        var data = Path.Combine(directory, Path.GetFileNameWithoutExtension(enlPath) + ".Data");
        if (!Directory.Exists(data))
            return null;

        var standard = Path.Combine(data, "sdb", "sdb.eni");
        if (IsReferenceDatabase(standard)) return standard;
        return Directory.EnumerateFiles(data, "*.eni", SearchOption.AllDirectories).FirstOrDefault(IsReferenceDatabase);
    }

    private static bool IsReferenceDatabase(string path)
    {
        if (!IsSqlite(path)) return false;
        using var connection = OpenReadOnly(path);
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='refs'";
        return Convert.ToInt32(check.ExecuteScalar()) > 0;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static ImportResult ReadSqlite(string path)
    {
        var result = new ImportResult();
        using var source = OpenReadOnly(path);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        // Include committed WAL records in one consistent snapshot without changing the library.
        source.BackupDatabase(connection);
        foreach (var collation in Collations)
        {
            try { connection.CreateCollation(collation, (left, right) => string.CompareOrdinal(left, right)); }
            catch { /* This library does not define that collation. */ }
        }

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'refs'";
            if (check.ExecuteScalar() is null)
                throw new InvalidDataException("这个文件不是可识别的 EndNote 文献库。");
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(refs)";
            using var reader = info.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        string[] wanted =
        [
            "id", "reference_type", "author", "year", "title", "secondary_title", "volume", "number",
            "pages", "publisher", "place_published", "language", "doi", "electronic_resource_number",
            "isbn_issn", "type_of_work", "trash_state", "abstract", "notes", "url",
        ];
        var selected = wanted.Where(columns.Contains).ToArray();
        if (!selected.Contains("title"))
            throw new InvalidDataException("EndNote 库里没有题名列。");

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT " + string.Join(", ", selected.Select(name => $"\"{name}\"")) + " FROM refs";
        using var rows = command.ExecuteReader();
        var index = 0;
        while (rows.Read())
        {
            index++;
            if (selected.Contains("trash_state"))
            {
                var trash = rows["trash_state"];
                if (trash is long trashLong && trashLong != 0)
                    continue;
                if (trash is int trashInt && trashInt != 0)
                    continue;
            }

            var title = Cell(rows, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                result.Warnings.Add($"跳过第 {index} 条：没有题名。");
                continue;
            }

            var type = MapSqliteType(rows, selected);
            var doi = Cell(rows, "doi");
            if (string.IsNullOrWhiteSpace(doi))
                doi = NormalizeDoi(Cell(rows, "electronic_resource_number"));

            var id = Cell(rows, "id");
            var item = new ReferenceItem
            {
                Type = type,
                Title = title,
                ContainerTitle = Cell(rows, "secondary_title"),
                Year = YearOf(Cell(rows, "year")),
                Volume = Cell(rows, "volume"),
                Issue = Cell(rows, "number"),
                Page = Cell(rows, "pages"),
                Publisher = Cell(rows, "publisher"),
                PublisherPlace = Cell(rows, "place_published"),
                Language = Cell(rows, "language"),
                Doi = doi,
                SourceKey = string.IsNullOrWhiteSpace(id) ? "" : "endnote-enl:" + id,
                Authors = SplitAuthors(Cell(rows, "author")),
            };
            if (!string.IsNullOrWhiteSpace(Cell(rows, "url")))
                item.Extra["url"] = Cell(rows, "url");
            result.Items.Add(item);
        }

        return result;
    }

    private static ImportResult ReadXml(string path)
    {
        var result = new ImportResult();
        var document = XDocument.Load(path);
        var records = document.Descendants().Where(element => element.Name.LocalName == "record");
        var index = 0;
        foreach (var record in records)
        {
            index++;
            var title = ChildText(record, "titles", "title");
            if (string.IsNullOrWhiteSpace(title))
                title = DirectChild(record, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                result.Warnings.Add($"跳过第 {index} 条：没有题名。");
                continue;
            }

            var typeName = record.Elements().FirstOrDefault(element => element.Name.LocalName == "ref-type")?.Attribute("name")?.Value;
            var container = ChildText(record, "titles", "secondary-title");
            if (string.IsNullOrWhiteSpace(container))
                container = ChildText(record, "periodical", "full-title");

            var authors = record.Descendants()
                .Where(element => element.Name.LocalName == "author")
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            var rec = DirectChild(record, "rec-number");
            var item = new ReferenceItem
            {
                Type = typeName is not null && XmlTypeMap.TryGetValue(typeName, out var mapped) ? mapped : "document",
                Title = title.Trim(),
                ContainerTitle = container.Trim(),
                Year = YearOf(ChildText(record, "dates", "year") is { Length: > 0 } dated ? dated : DirectChild(record, "year")),
                Volume = DirectChild(record, "volume"),
                Issue = DirectChild(record, "number"),
                Page = DirectChild(record, "pages"),
                Publisher = DirectChild(record, "publisher"),
                PublisherPlace = DirectChild(record, "pub-location"),
                Language = DirectChild(record, "language"),
                Doi = NormalizeDoi(DirectChild(record, "electronic-resource-num")),
                SourceKey = string.IsNullOrWhiteSpace(rec) ? "" : "endnote-xml:" + rec.Trim(),
                Authors = authors.Select(AuthorFormatter.Parse).ToList(),
            };
            result.Items.Add(item);
        }

        return result;
    }

    private static string MapSqliteType(SqliteDataReader reader, IReadOnlyCollection<string> columns)
    {
        var work = Cell(reader, "type_of_work");
        if (work.Contains("学位", StringComparison.Ordinal) || work.Contains("thesis", StringComparison.OrdinalIgnoreCase))
            return "thesis";

        if (!columns.Contains("reference_type"))
            return "document";

        var raw = reader["reference_type"];
        var number = raw switch
        {
            long value => (int)value,
            int value => value,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => -1,
        };
        return ReferenceTypeMap.TryGetValue(number, out var mapped) ? mapped : "document";
    }

    private static List<Author> SplitAuthors(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(AuthorFormatter.Parse)
            .ToList();
    }

    private static string YearOf(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"\d{4}");
        return match.Success ? match.Value : raw.Trim();
    }

    private static string NormalizeDoi(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
            return "";
        value = value.Replace("https://doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://doi.org/", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return value.StartsWith("10.", StringComparison.Ordinal) ? value : "";
    }

    private static string Cell(SqliteDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? "";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }
    }

    private static string DirectChild(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim() ?? "";

    private static string ChildText(XElement parent, string parentName, string childName)
    {
        var container = parent.Elements().FirstOrDefault(element => element.Name.LocalName == parentName);
        if (container is null)
            return "";
        return container.Elements().FirstOrDefault(element => element.Name.LocalName == childName)?.Value.Trim() ?? "";
    }

    private static bool IsSqlite(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[16];
            if (stream.Read(buffer, 0, buffer.Length) < buffer.Length)
                return false;
            return Encoding.ASCII.GetString(buffer).StartsWith("SQLite format 3", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
