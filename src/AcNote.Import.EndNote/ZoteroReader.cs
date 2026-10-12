using AcNote.Core;
using Microsoft.Data.Sqlite;

namespace AcNote.Import.EndNote;

internal static class ZoteroReader
{
    public static ImportResult Read(string path)
    {
        // SQLite backup reads a consistent snapshot, including committed WAL records.
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        source.Open();
        using var snapshot = new SqliteConnection("Data Source=:memory:");
        snapshot.Open();
        source.BackupDatabase(snapshot);
        var result = new ImportResult();
        using var query = snapshot.CreateCommand();
        query.CommandText = "SELECT i.itemID, t.typeName, i.libraryID, i.key FROM items i JOIN itemTypes t ON i.itemTypeID=t.itemTypeID WHERE t.typeName NOT IN ('attachment','note','annotation') AND NOT EXISTS (SELECT 1 FROM deletedItems d WHERE d.itemID=i.itemID) ORDER BY i.itemID";
        using var rows = query.ExecuteReader();
        while (rows.Read())
        {
            var id = rows.GetInt64(0);
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var data = snapshot.CreateCommand())
            {
                data.CommandText = "SELECT f.fieldName, v.value FROM itemData d JOIN fields f ON d.fieldID=f.fieldID JOIN itemDataValues v ON d.valueID=v.valueID WHERE d.itemID=$id";
                data.Parameters.AddWithValue("$id", id);
                using var values = data.ExecuteReader();
                while (values.Read()) fields[values.GetString(0)] = values.IsDBNull(1) ? "" : values.GetValue(1).ToString() ?? "";
            }
            string Get(params string[] names) => names.Select(n => fields.GetValueOrDefault(n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
            if (Get("title").Length == 0) { result.Warnings.Add($"Zotero 记录 {id} 没有题名，已跳过。"); continue; }
            var item = new ReferenceItem { SourceKey = $"zotero:{rows.GetInt64(2)}:{rows.GetString(3)}", Type = ReferenceImporter.TypeOf(rows.GetString(1)),
                Title = Get("title"), ContainerTitle = Get("publicationTitle", "bookTitle", "proceedingsTitle"), Year = ReferenceImporter.Year(Get("date")),
                Volume = Get("volume"), Issue = Get("issue"), Page = Get("pages"), Publisher = Get("publisher", "university", "institution"),
                PublisherPlace = Get("place"), Doi = Get("DOI"), Url = Get("url"), Language = Get("language"), Extra = fields };
            using (var creators = snapshot.CreateCommand())
            {
                creators.CommandText = "SELECT c.firstName,c.lastName,c.fieldMode FROM itemCreators ic JOIN creators c ON ic.creatorID=c.creatorID JOIN creatorTypes ct ON ic.creatorTypeID=ct.creatorTypeID WHERE ic.itemID=$id AND ct.creatorType='author' ORDER BY ic.orderIndex";
                creators.Parameters.AddWithValue("$id", id);
                using var names = creators.ExecuteReader();
                while (names.Read())
                {
                    var given = names.IsDBNull(0) ? "" : names.GetString(0);
                    var family = names.IsDBNull(1) ? "" : names.GetString(1);
                    item.Authors.Add(names.GetInt64(2) == 1 ? new Author { Literal = family } : new Author { Given = given, Family = family });
                }
            }
            result.Items.Add(item);
        }
        return result;
    }
}
