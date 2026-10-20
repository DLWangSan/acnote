using AcNote.Import.EndNote;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AcNote.Import.EndNote.Tests;

public sealed class ReferenceImporterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("acnote-import-test-").FullName;
    private string Write(string name, string text) { var path = Path.Combine(_directory, name); File.WriteAllText(path, text); return path; }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(_directory, true); }

    [Fact]
    public void BibTex_PreservesNestedTextAuthorsMacrosAndCrossref()
    {
        var path = Write("test.bib", """
            % ignored @book{fake}
            @string{j = "Journal of " # "Science"}
            @article{key, title={A {DNA} study, with punctuation}, author={Garc{\'i}a, Ana and {Research and Development} and 张三}, journal=j, year=2024, pages={1--9}, doi={https://doi.org/10.1/test}}
            @inproceedings{child, title="A paper", crossref={parent}}
            @book{parent,title={Proceedings},publisher={Press},year={2020}}
            """);
        var result = ReferenceImporter.Import(path);
        Assert.Equal(3, result.Items.Count);
        var item = result.Items[0];
        Assert.Equal("A DNA study, with punctuation", item.Title);
        Assert.Equal("Journal of Science", item.ContainerTitle);
        Assert.Equal("García", item.Authors[0].Family);
        Assert.Equal("Research and Development", item.Authors[1].Literal);
        Assert.Equal(3, item.Authors.Count);
        Assert.Equal("1-9", item.Page);
        Assert.Equal("10.1/test", item.Doi);
        Assert.Equal("Proceedings", result.Items[1].ContainerTitle);
        Assert.Equal("2020", result.Items[1].Year);
    }

    [Fact]
    public void MalformedBibTex_DoesNotSilentlyReturnPartialImport()
    {
        var path = Write("broken.bib", "@book{one,title={valid}} @article{two,title={unfinished");
        Assert.Throws<InvalidDataException>(() => ReferenceImporter.Import(path));
    }

    [Fact]
    public void Ris_ReadsRepeatedAuthorsContinuationAndMultipleRecords()
    {
        var path = Write("test.ris", "TY  - JOUR\nTI  - First line\n      second line\nAU  - Smith, John\nAU  - 张三\nPY  - 2023/05/01\nSP  - 9\nEP  - 12\nUR  - https://example.org\nER  -\nTY  - BOOK\nTI  - Book\nER  -\n");
        var result = ReferenceImporter.Import(path);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("First line second line", result.Items[0].Title);
        Assert.Equal(2, result.Items[0].Authors.Count);
        Assert.Equal("2023", result.Items[0].Year);
        Assert.Equal("9-12", result.Items[0].Page);
        Assert.Equal("book", result.Items[1].Type);
    }

    [Fact]
    public void CslJson_MapsDatesLiteralAuthorsAndDoi()
    {
        var path = Write("zotero.json", """
            [{"id":"1","type":"article-journal","title":"中文题名","issued":{"date-parts":[[2022,5,1]]},"author":[{"literal":"研究组"},{"family":"Smith","given":"John"}],"DOI":"doi:10.123/a","URL":"https://example.org","edition":"2"}]
            """);
        var item = Assert.Single(ReferenceImporter.Import(path).Items);
        Assert.Equal("2022", item.Year); Assert.Equal("研究组", item.Authors[0].Literal);
        Assert.Equal("10.123/a", item.Doi); Assert.Equal("2", item.Extra["edition"]);
    }

    [Fact]
    public void CslJson_PreprintMapsArchiveAndArchiveIdentifierAliases()
    {
        var path = Write("preprint.json", """
            [{"id":"arxiv-1","type":"article-preprint","title":"A preprint","archive":"arXiv","archive-id":"2410.12345","URL":"https://arxiv.org/abs/2410.12345"}]
            """);

        var item = Assert.Single(ReferenceImporter.Import(path).Items);

        Assert.Equal("preprint", item.Type);
        Assert.Equal("arXiv", item.ContainerTitle);
        Assert.Equal("2410.12345", item.Extra["archive-id"]);
        Assert.Equal("https://arxiv.org/abs/2410.12345", item.Url);
    }

    [Fact]
    public void LegacyChineseRis_DecodesWithoutReplacementCharacters()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var path = Path.Combine(_directory, "chinese.ris");
        File.WriteAllText(path, "TY  - JOUR\r\nTI  - 中文标题\r\nAU  - 张三\r\nER  -\r\n", System.Text.Encoding.GetEncoding("GB18030"));
        var item = Assert.Single(ReferenceImporter.Import(path).Items);
        Assert.Equal("中文标题", item.Title); Assert.Equal("张三", item.Authors[0].Literal);
    }

    [Fact]
    public void SourceKeys_AreStableButDoNotCollideAcrossLibraries()
    {
        var first = Write("first.bib", "@book{1,title={First}}");
        var second = Write("second.bib", "@book{1,title={Second}}");
        var key = Assert.Single(ReferenceImporter.Import(first).Items).SourceKey;
        File.WriteAllText(first, "@book{1,title={Updated}}");
        Assert.Equal(key, Assert.Single(ReferenceImporter.Import(first).Items).SourceKey);
        Assert.NotEqual(key, Assert.Single(ReferenceImporter.Import(second).Items).SourceKey);
    }

    [Fact]
    public void EndNote_SplitLibrarySkipsEnlAndPdfIndexesAndImportsEntireLibrary()
    {
        var enl = Path.Combine(_directory, "split.enl");
        var data = Path.Combine(_directory, "split.Data", "sdb");
        Directory.CreateDirectory(data);
        void Create(string path, string sql)
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            db.Open(); using var command = db.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
        }
        Create(enl, "CREATE TABLE enl_refs(id INTEGER);");
        Create(Path.Combine(data, "pdb.eni"), "CREATE TABLE pdf_index(id INTEGER);");
        Create(Path.Combine(data, "sdb.eni"), "CREATE TABLE refs(id INTEGER,title TEXT,author TEXT,year TEXT,trash_state INTEGER); INSERT INTO refs VALUES(1,'First','Smith, John','2020',0),(2,'第二条','张三','2021',0),(3,'Deleted','','',1);");
        var before = File.ReadAllBytes(Path.Combine(data, "sdb.eni"));
        var items = EndNoteImporter.Import(enl).Items;
        Assert.Equal(2, items.Count);
        Assert.Equal("第二条", items[1].Title);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(data, "sdb.eni")));
        var archive = Path.Combine(_directory, "split.enlx");
        // Archive outside the fixture folder to avoid including itself.
        var staging = Path.Combine(_directory, "archive-content");
        Directory.CreateDirectory(Path.Combine(staging, "split.Data", "sdb"));
        File.Copy(enl, Path.Combine(staging, "split.enl"));
        foreach (var file in Directory.GetFiles(data)) File.Copy(file, Path.Combine(staging, "split.Data", "sdb", Path.GetFileName(file)));
        System.IO.Compression.ZipFile.CreateFromDirectory(staging, archive);
        Assert.Equal(2, EndNoteImporter.Import(archive).Items.Count);
    }

    [Fact]
    public void Zotero_ReadsWalSnapshotWithoutChangingSourceAndSkipsTrashAndAttachments()
    {
        var path = Path.Combine(_directory, "zotero.sqlite");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE items(itemID INTEGER,itemTypeID INTEGER,libraryID INTEGER,key TEXT);
            CREATE TABLE itemTypes(itemTypeID INTEGER,typeName TEXT);
            CREATE TABLE fields(fieldID INTEGER,fieldName TEXT);
            CREATE TABLE itemData(itemID INTEGER,fieldID INTEGER,valueID INTEGER);
            CREATE TABLE itemDataValues(valueID INTEGER,value TEXT);
            CREATE TABLE deletedItems(itemID INTEGER);
            CREATE TABLE creators(creatorID INTEGER,firstName TEXT,lastName TEXT,fieldMode INTEGER);
            CREATE TABLE itemCreators(itemID INTEGER,creatorID INTEGER,creatorTypeID INTEGER,orderIndex INTEGER);
            CREATE TABLE creatorTypes(creatorTypeID INTEGER,creatorType TEXT);
            INSERT INTO itemTypes VALUES(1,'journalArticle'),(2,'attachment');
            INSERT INTO items VALUES(1,1,1,'ABC'),(2,1,1,'TRASH'),(3,2,1,'PDF');
            INSERT INTO deletedItems VALUES(2);
            INSERT INTO fields VALUES(1,'title'),(2,'date'),(3,'DOI');
            INSERT INTO itemDataValues VALUES(1,'Zotero Paper'),(2,'2021-06-01'),(3,'10.1/z');
            INSERT INTO itemData VALUES(1,1,1),(1,2,2),(1,3,3);
            INSERT INTO creators VALUES(1,'John','Smith',0),(2,'','Research Team',1),(3,'Editor','Only',0);
            INSERT INTO creatorTypes VALUES(1,'author'),(2,'editor');
            INSERT INTO itemCreators VALUES(1,2,1,0),(1,1,1,1),(1,3,2,2);
            """;
        command.ExecuteNonQuery();
        byte[] Snapshot()
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
        }
        var before = Snapshot();
        var item = Assert.Single(ReferenceImporter.Import(path).Items);
        Assert.Equal("Zotero Paper", item.Title); Assert.Equal("2021", item.Year);
        Assert.Equal(2, item.Authors.Count); Assert.Equal("Research Team", item.Authors[0].Literal);
        Assert.Equal("Smith", item.Authors[1].Family);
        Assert.Equal(before, Snapshot());
    }
}
