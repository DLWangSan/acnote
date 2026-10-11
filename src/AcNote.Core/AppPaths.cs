namespace AcNote.Core;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcNote");

    public static string Styles => Path.Combine(Root, "styles");

    public static string Database => Path.Combine(Root, "library.db");

    public static string AddinLog => Path.Combine(Root, "addin.log");

    public static void EnsureStyles(string bundledDirectory)
    {
        Directory.CreateDirectory(Styles);
        foreach (var file in Directory.EnumerateFiles(Styles, "*.style"))
        {
            if (File.ReadAllText(file).Contains("sysu-thesis", StringComparison.Ordinal))
                File.Delete(file);
        }

        if (!Directory.Exists(bundledDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(bundledDirectory, "*.style"))
        {
            var destination = Path.Combine(Styles, Path.GetFileName(file));
            if (!File.Exists(destination))
                File.Copy(file, destination);
        }
    }
}
