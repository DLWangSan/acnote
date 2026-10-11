using AcNote.Core;

namespace AcNote.App;

public sealed class Session
{
    public Session(LibraryStore store, StyleCatalog catalog)
    {
        Store = store;
        Catalog = catalog;
        StyleId = catalog.All.Any(style => style.Id == StyleCatalog.DefaultId)
            ? StyleCatalog.DefaultId
            : catalog.All.FirstOrDefault()?.Id ?? StyleCatalog.DefaultId;
    }

    public LibraryStore Store { get; }
    public StyleCatalog Catalog { get; private set; }
    public string StyleId { get; set; }

    public ResolvedStyle CurrentStyle => Catalog.Get(StyleId);

    public void ReloadStyles() =>
        Catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"), AppPaths.Styles);
}
