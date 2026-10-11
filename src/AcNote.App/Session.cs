using AcNote.Core;

namespace AcNote.App;

public sealed class Session
{
    public Session(LibraryStore? store, StyleCatalog catalog, string? libraryPath)
    {
        Store = store;
        Catalog = catalog;
        LibraryPath = libraryPath;
        StyleId = catalog.All.Any(style => style.Id == StyleCatalog.DefaultId)
            ? StyleCatalog.DefaultId
            : catalog.All.FirstOrDefault()?.Id ?? StyleCatalog.DefaultId;
    }

    public LibraryStore? Store { get; private set; }
    public string? LibraryPath { get; private set; }
    public StyleCatalog Catalog { get; private set; }
    public string StyleId { get; set; }

    public ResolvedStyle CurrentStyle => Catalog.Get(StyleId);

    public void UseLibrary(LibraryStore store, string path)
    {
        Store = store;
        LibraryPath = path;
        WorkspaceSettings.RememberLibrary(path);
    }

    public void ReloadStyles() =>
        Catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"), AppPaths.Styles);
}
