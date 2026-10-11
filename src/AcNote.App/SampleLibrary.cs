using AcNote.Core;

namespace AcNote.App;

public static class SampleLibrary
{
    public static void Seed(LibraryStore store)
    {
        store.Save(new ReferenceItem
        {
            Id = "yuan-1966",
            Type = "article-journal",
            Title = "水稻的雄性不孕性",
            ContainerTitle = "科学通报",
            Year = "1966",
            Volume = "17",
            Issue = "4",
            Page = "185-188",
            Authors = [AuthorFormatter.Parse("袁隆平")],
        });
        store.Save(new ReferenceItem
        {
            Id = "liu-1979",
            Type = "article-journal",
            Title = "青蒿素(Arteannuin)的结构和反应",
            ContainerTitle = "化学学报",
            Language = "zh",
            Year = "1979",
            Volume = "37",
            Issue = "2",
            Page = "129-143",
            Authors =
            [
                AuthorFormatter.Parse("刘静明"),
                AuthorFormatter.Parse("倪慕云"),
                AuthorFormatter.Parse("樊菊芬"),
                AuthorFormatter.Parse("屠呦呦"),
                AuthorFormatter.Parse("吴照华"),
                AuthorFormatter.Parse("吴毓林"),
                AuthorFormatter.Parse("周维善"),
            ],
        });
        store.Save(new ReferenceItem
        {
            Id = "watson-1953",
            Type = "article-journal",
            Title = "Molecular structure of nucleic acids: a structure for deoxyribose nucleic acid",
            ContainerTitle = "Nature",
            Year = "1953",
            Volume = "171",
            Issue = "4356",
            Page = "737-738",
            Doi = "10.1038/171737a0",
            Authors =
            [
                AuthorFormatter.Parse("Watson, James D"),
                AuthorFormatter.Parse("Crick, Francis H C"),
            ],
        });
        store.Save(new ReferenceItem
        {
            Id = "wang-1960",
            Type = "book",
            Title = "人间词话",
            PublisherPlace = "北京",
            Publisher = "人民文学出版社",
            Year = "1960",
            Authors = [AuthorFormatter.Parse("王国维")],
        });
        store.Save(new ReferenceItem
        {
            Id = "turing-1938",
            Type = "thesis",
            Title = "Systems of Logic Based on Ordinals",
            PublisherPlace = "Princeton",
            Publisher = "Princeton University",
            Year = "1938",
            Authors = [AuthorFormatter.Parse("Turing, Alan M")],
        });
        store.Save(new ReferenceItem
        {
            Id = "vaswani-2017",
            Type = "paper-conference",
            Title = "Attention is all you need",
            ContainerTitle = "Advances in Neural Information Processing Systems",
            Year = "2017",
            Page = "5998-6008",
            Authors =
            [
                AuthorFormatter.Parse("Vaswani, Ashish"),
                AuthorFormatter.Parse("Shazeer, Noam"),
                AuthorFormatter.Parse("Parmar, Niki"),
                AuthorFormatter.Parse("Uszkoreit, Jakob"),
                AuthorFormatter.Parse("Jones, Llion"),
                AuthorFormatter.Parse("Gomez, Aidan N"),
                AuthorFormatter.Parse("Kaiser, Lukasz"),
                AuthorFormatter.Parse("Polosukhin, Illia"),
            ],
        });
        store.Save(new ReferenceItem
        {
            Id = "mnist-1998",
            Type = "dataset",
            Title = "The MNIST database of handwritten digits",
            Year = "1998",
            Url = "http://yann.lecun.com/exdb/mnist/",
            Authors =
            [
                AuthorFormatter.Parse("LeCun, Yann"),
                AuthorFormatter.Parse("Cortes, Corinna"),
                AuthorFormatter.Parse("Burges, Christopher J C"),
            ],
        });
    }
}
