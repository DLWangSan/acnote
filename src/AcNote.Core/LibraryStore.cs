using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AcNote.Core;

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly object _gate = new();

    public LibraryStore(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        IsNew = !File.Exists(path);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS refs (
              id TEXT PRIMARY KEY,
              type TEXT NOT NULL,
              title TEXT,
              container_title TEXT,
              year TEXT,
              volume TEXT,
              issue TEXT,
              page TEXT,
              publisher TEXT,
              publisher_place TEXT,
              doi TEXT,
              language TEXT,
              authors_json TEXT NOT NULL,
              source_key TEXT,
              extra_json TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_refs_source ON refs(source_key);
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "url", "TEXT");
    }

    public bool IsNew { get; }

    public int Count()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM refs";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void Checkpoint()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using var reader = command.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) != 0)
                throw new IOException("文献库正在被其他程序写入，暂时无法完成单文件保存。请关闭其他 AcNote 窗口后重试。");
        }
    }

    public IReadOnlyList<ReferenceItem> List()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM refs ORDER BY title";
            using var reader = command.ExecuteReader();
            var items = new List<ReferenceItem>();
            while (reader.Read())
                items.Add(Read(reader));
            return items;
        }
    }

    public ReferenceItem? Find(string id)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM refs WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        }
    }

    public ReferenceItem? FindBySource(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return null;

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM refs WHERE source_key = $source LIMIT 1";
            command.Parameters.AddWithValue("$source", sourceKey);
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        }
    }

    public void Save(ReferenceItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = Guid.NewGuid().ToString("N");

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO refs (
                  id, type, title, container_title, year, volume, issue, page,
                  publisher, publisher_place, doi, url, language, authors_json, source_key, extra_json
                ) VALUES (
                  $id, $type, $title, $container, $year, $volume, $issue, $page,
                  $publisher, $place, $doi, $url, $language, $authors, $source, $extra
                )
                ON CONFLICT(id) DO UPDATE SET
                  type = excluded.type,
                  title = excluded.title,
                  container_title = excluded.container_title,
                  year = excluded.year,
                  volume = excluded.volume,
                  issue = excluded.issue,
                  page = excluded.page,
                  publisher = excluded.publisher,
                  publisher_place = excluded.publisher_place,
                  doi = excluded.doi,
                  url = excluded.url,
                  language = excluded.language,
                  authors_json = excluded.authors_json,
                  source_key = excluded.source_key,
                  extra_json = excluded.extra_json
                """;
            Bind(command, item);
            command.ExecuteNonQuery();
        }
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM refs WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static void Bind(SqliteCommand command, ReferenceItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$type", string.IsNullOrWhiteSpace(item.Type) ? "document" : item.Type);
        command.Parameters.AddWithValue("$title", item.Title ?? "");
        command.Parameters.AddWithValue("$container", item.ContainerTitle ?? "");
        command.Parameters.AddWithValue("$year", item.Year ?? "");
        command.Parameters.AddWithValue("$volume", item.Volume ?? "");
        command.Parameters.AddWithValue("$issue", item.Issue ?? "");
        command.Parameters.AddWithValue("$page", item.Page ?? "");
        command.Parameters.AddWithValue("$publisher", item.Publisher ?? "");
        command.Parameters.AddWithValue("$place", item.PublisherPlace ?? "");
        command.Parameters.AddWithValue("$doi", item.Doi ?? "");
        command.Parameters.AddWithValue("$url", item.Url ?? "");
        command.Parameters.AddWithValue("$language", item.Language ?? "");
        command.Parameters.AddWithValue("$authors", JsonSerializer.Serialize(item.Authors, JsonOptions));
        command.Parameters.AddWithValue("$source", item.SourceKey ?? "");
        command.Parameters.AddWithValue("$extra", JsonSerializer.Serialize(item.Extra, JsonOptions));
    }

    private static ReferenceItem Read(SqliteDataReader reader)
    {
        var authors = JsonSerializer.Deserialize<List<Author>>(reader.GetString(reader.GetOrdinal("authors_json")), JsonOptions) ?? [];
        var extraText = reader.IsDBNull(reader.GetOrdinal("extra_json")) ? "{}" : reader.GetString(reader.GetOrdinal("extra_json"));
        var extra = JsonSerializer.Deserialize<Dictionary<string, string>>(extraText, JsonOptions) ?? new Dictionary<string, string>();
        return new ReferenceItem
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Title = ReadString(reader, "title"),
            ContainerTitle = ReadString(reader, "container_title"),
            Year = ReadString(reader, "year"),
            Volume = ReadString(reader, "volume"),
            Issue = ReadString(reader, "issue"),
            Page = ReadString(reader, "page"),
            Publisher = ReadString(reader, "publisher"),
            PublisherPlace = ReadString(reader, "publisher_place"),
            Doi = ReadString(reader, "doi"),
            Url = ReadString(reader, "url"),
            Language = ReadString(reader, "language"),
            SourceKey = ReadString(reader, "source_key"),
            Authors = authors,
            Extra = extra,
        };
    }

    private static void EnsureColumn(SqliteConnection connection, string column, string type)
    {
        using var info = connection.CreateCommand();
        info.CommandText = "PRAGMA table_info(refs)";
        using var reader = info.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE refs ADD COLUMN {column} {type}";
        alter.ExecuteNonQuery();
    }

    private static string ReadString(SqliteDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }
    }
}
