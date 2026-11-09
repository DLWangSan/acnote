using System.Text.Json.Nodes;

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
    }

    public static bool IsLegacySchoolPlaceholder(string json)
    {
        try
        {
            var node = JsonNode.Parse(json)!.AsObject();
            return node["id"]?.GetValue<string>() is "sysu" or "thu" or "pku"
                && node["status"]?.GetValue<string>() == "unverified"
                && node.All(p => new[] { "id", "name", "extends", "group", "status" }.Contains(p.Key));
        }
        catch { return false; }
    }
}
