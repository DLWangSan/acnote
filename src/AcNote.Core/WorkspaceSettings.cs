using System.Text.Json;

namespace AcNote.Core;

public static class WorkspaceSettings
{
    private static string FilePath => Path.Combine(AppPaths.Root, "workspace.json");

    public static string? LibraryPath
    {
        get
        {
            if (!File.Exists(FilePath))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.TryGetProperty("libraryPath", out var value) ? value.GetString() : null;
        }
    }

    public static void RememberLibrary(string path)
    {
        Directory.CreateDirectory(AppPaths.Root);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { libraryPath = path }));
    }
}
