using AcNote.Import.EndNote;
using Xunit;
using Microsoft.Data.Sqlite;

namespace AcNote.Import.EndNote.Tests;

public class EndNoteImporterTests
{
    [Fact]
    public void SqliteLibrary_ImportsAuthorsAndSkipsTrash()
    {
        var path = Path.Combine(Path.GetTempPath(), "acnote-" + Guid.NewGuid().ToString("N") + ".enl");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE refs (
                  id INTEGER,
                  reference_type INTEGER,
                  author TEXT,
                  year TEXT,
                  title TEXT,
                  secondary_title TEXT,
                  volume TEXT,
                  number TEXT,
                  pages TEXT,
                  publisher TEXT,
                  place_published TEXT,
                  language TEXT,
                  trash_state INTEGER
                );
                INSERT INTO refs VALUES (7, 0, 'Smith, John' || char(13) || '张三', '2021', 'Mixed author paper', 'Demo Journal', '3', '1', '9-12', '', '', 'English', 0);
                INSERT INTO refs VALUES (8, 0, 'Nobody', '2020', 'Deleted', 'Demo Journal', '', '', '', '', '', '', 1);
                """;
            command.ExecuteNonQuery();
        }

        var result = EndNoteImporter.Import(path);
        var item = Assert.Single(result.Items);
        Assert.Equal("endnote-enl:7", item.SourceKey);
        Assert.Equal("article-journal", item.Type);
        Assert.Equal("Mixed author paper", item.Title);
        Assert.Equal("3", item.Volume);
        Assert.Equal("1", item.Issue);
        Assert.Equal(2, item.Authors.Count);
        Assert.Equal("Smith", item.Authors[0].Family);
        Assert.Equal("张三", item.Authors[1].Literal);
    }

    [Fact]
    public void XmlExport_ImportsJournalRecord()
    {
        var path = Path.Combine(Path.GetTempPath(), "acnote-" + Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path,
            """
            <xml>
              <records>
                <record>
                  <rec-number>15</rec-number>
                  <ref-type name="Journal Article">17</ref-type>
                  <contributors><authors><author>Wang, San</author><author>李四</author></authors></contributors>
                  <titles><title>示例题名</title><secondary-title>示例学报</secondary-title></titles>
                  <dates><year>2018</year></dates>
                  <volume>2</volume>
                  <number>9</number>
                  <pages>4-8</pages>
                  <language>Chinese</language>
                </record>
              </records>
            </xml>
            """);

        var result = EndNoteImporter.Import(path);
        var item = Assert.Single(result.Items);
        Assert.Equal("endnote-xml:15", item.SourceKey);
        Assert.Equal("article-journal", item.Type);
        Assert.Equal("示例学报", item.ContainerTitle);
        Assert.Equal("2018", item.Year);
        Assert.Equal("Chinese", item.Language);
        Assert.Equal(2, item.Authors.Count);
    }

    [Fact]
    public void PlainText_IsRejectedWithAClearMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), "acnote-" + Guid.NewGuid().ToString("N") + ".enl");
        File.WriteAllText(path, "this is not a library");
        var error = Assert.Throws<InvalidDataException>(() => EndNoteImporter.Import(path));
        Assert.Contains("EndNote XML", error.Message);
    }

    [Fact]
    public void TravelingLibraryFragment_ImportsEmbeddedCitationRecord()
    {
        const string fieldCode = "ADDIN EN.CITE <EndNote><Cite><Author>Wang</Author><Year>2020</Year><RecNum>15</RecNum><record><rec-number>15</rec-number><ref-type name=\"Journal Article\">17</ref-type><contributors><authors><author>Wang, San</author></authors></contributors><titles><title>Embedded paper</title><secondary-title>Journal</secondary-title></titles><dates><year>2020</year></dates><electronic-resource-num>10.1234/demo</electronic-resource-num></record></Cite></EndNote>";

        var result = EndNoteImporter.ImportTravelingLibrary(fieldCode);

        var item = Assert.Single(result.Items);
        Assert.Equal("endnote-xml:15", item.SourceKey);
        Assert.Equal("Embedded paper", item.Title);
        Assert.Equal("2020", item.Year);
        Assert.Equal("10.1234/demo", item.Doi);
        Assert.Equal("Wang", item.Authors[0].Family);
    }

    [Fact]
    public void UnformattedCitation_ExtractsRecordNumbersFromTemporaryCitationSyntax()
    {
        var numbers = EndNoteImporter.ExtractCitationRecordNumbers("ADDIN EN.CITE {Smith, 2020 #25; Wang, 2022 #31}");

        Assert.Equal(new[] { "25", "31" }, numbers);
    }

    [Fact]
    public void FormattedCitation_ExtractsRecordNumbersFromXmlTags()
    {
        var numbers = EndNoteImporter.ExtractCitationRecordNumbers("ADDIN EN.CITE <EndNote><Cite><RecNum>25</RecNum><record><rec-number>25</rec-number></record></Cite></EndNote>");

        Assert.Equal(new[] { "25" }, numbers);
    }
}
