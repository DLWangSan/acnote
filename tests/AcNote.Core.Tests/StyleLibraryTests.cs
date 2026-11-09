using System.Text.Json.Nodes;
using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public sealed class StyleLibraryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("acnote-style-library-").FullName;
    private string Bundled => Path.Combine(_root, "bundled");
    private string User => Path.Combine(_root, "user");
    private StyleLibrary Library => new(Bundled, User);
    private const string Original = """{"id":"base","name":"内置版式","layout":"author-year","etAl":{"minCount":4,"useFirst":3,"authorSeparator":"; ","familyInitialsSeparator":", ","initialsUsePeriods":true},"templates":{"article-journal":[{"field":"author"}]}}""";

    public StyleLibraryTests()
    {
        Directory.CreateDirectory(Bundled); Directory.CreateDirectory(User);
        File.WriteAllText(Path.Combine(Bundled, "base.style"), Original);
    }

    [Fact]
    public void BuiltinsCannotBeOverwrittenRenamedOrDeleted()
    {
        var original = Library.Load().Get("base");
        Assert.Throws<InvalidOperationException>(() => Library.Save(original, Original));
        Assert.Throws<InvalidOperationException>(() => Library.Rename(original, "另一个名称"));
        Assert.Throws<InvalidOperationException>(() => Library.Delete(original));
        Assert.Throws<InvalidOperationException>(() => StyleCatalog.Install(Original, User, Bundled));
        Assert.Equal(Original, File.ReadAllText(Path.Combine(Bundled, "base.style")));
    }

    [Fact]
    public void NamedCopyIsIndependentAndRenamePreservesDocumentStyleId()
    {
        var id = Library.CreateCopy(Library.Load().Get("base"), "学校版式");
        var copy = Library.Load().Get(id);
        Assert.Equal("; ", copy.EtAl.AuthorSeparator);
        Assert.True(copy.EtAl.InitialsUsePeriods);
        Library.Rename(copy, "期刊版式");
        var renamed = Library.Load().Get(id);
        Assert.Equal("期刊版式", renamed.Name);
        Assert.Equal(id, renamed.Id);
        var node = StyleLibrary.Snapshot(renamed);
        node["id"] = "base"; node["name"] = "不能改掉稳定标识";
        node["etAl"]!["authorSeparator"] = "，";
        Library.Save(renamed, node.ToJsonString());
        Assert.Equal("，", Library.Load().Get(id).EtAl.AuthorSeparator);
        Assert.Equal("期刊版式", Library.Load().Get(id).Name);
        Assert.Equal("; ", Library.Load().Get("base").EtAl.AuthorSeparator);
        Assert.Throws<InvalidOperationException>(() => Library.CreateCopy(renamed, "期刊版式"));
        Assert.Null(JsonNode.Parse(File.ReadAllText(Library.Load().Get(id).FilePath!))!["extends"]);
        Library.Delete(Library.Load().Get(id));
        Assert.DoesNotContain(Library.Load().All, s => s.Id == id);
    }

    [Fact]
    public void LegacyOverridesAndDependentStylesArePreservedOnce()
    {
        var changed = JsonNode.Parse(Original)!;
        changed["etAl"]!["authorSeparator"] = "，";
        File.WriteAllText(Path.Combine(User, "base.style"), changed.ToJsonString());
        File.WriteAllText(Path.Combine(User, "child.style"), """{"id":"child","name":"旧版派生","extends":"base"}""");
        var loaded = Library.Load();
        Assert.Equal("; ", loaded.Get("base").EtAl.AuthorSeparator);
        var preserved = Assert.Single(loaded.All, s => s.Name.EndsWith("（旧版自定义）"));
        Assert.Equal("，", preserved.EtAl.AuthorSeparator);
        Assert.Equal("，", loaded.Get("child").EtAl.AuthorSeparator);
        Assert.Equal(preserved.Id, JsonNode.Parse(File.ReadAllText(Path.Combine(User, "child.style")))!["extends"]!.GetValue<string>());
        Assert.Equal(3, Library.Load().All.Count);
        Assert.NotEmpty(Directory.GetFiles(User, "*.bak"));
    }

    [Fact]
    public void ImportNeverOverwritesExistingBuiltinOrCustomStyle()
    {
        var first = Library.Import(Original);
        var second = Library.Import(Original);
        Assert.NotEqual(first, second);
        Assert.NotEqual("base", first);
        Assert.Equal(3, Library.Load().All.Count);
        Assert.Equal(3, Library.Load().All.Select(s => s.Name).Distinct().Count());
    }

    [Fact]
    public void UnchangedLegacyCopiesAreRetiredWithoutAddingDuplicateStyles()
    {
        File.WriteAllText(Path.Combine(User, "base.style"), Original);
        Assert.Single(Library.Load().All);
        Assert.Empty(Directory.GetFiles(User, "*.style"));
    }

    [Fact]
    public void RenameHandlesNonCanonicalFileNamesAndDeletionProtectsDependencies()
    {
        File.WriteAllText(Path.Combine(User, "shared-file.style"), """{"id":"custom","name":"分享版式"}""");
        Library.Rename(Library.Load().Get("custom"), "改名版式");
        Assert.Equal("改名版式", Library.Load().Get("custom").Name);
        Assert.Single(Directory.GetFiles(User, "*.style"));
        File.WriteAllText(Path.Combine(User, "child.style"), """{"id":"child","name":"派生版式","extends":"custom"}""");
        Assert.Throws<InvalidOperationException>(() => Library.Delete(Library.Load().Get("custom")));
        Assert.True(File.Exists(Path.Combine(User, "custom.style")));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
