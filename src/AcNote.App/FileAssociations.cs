using Microsoft.Win32;
using AcNote.Import.EndNote;

namespace AcNote.App;

internal static class FileAssociations
{
    public static void Register()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        using var app = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\AcNote.App.exe");
        app.SetValue("FriendlyAppName", "AcNote");
        using (var command = app.CreateSubKey(@"shell\open\command")) command.SetValue("", $"\"{exe}\" \"%1\"");
        using var supported = app.CreateSubKey("SupportedTypes");
        using var prog = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AcNote.Reference");
        prog.SetValue("", "AcNote 文献文件");
        using (var command = prog.CreateSubKey(@"shell\open\command")) command.SetValue("", $"\"{exe}\" \"%1\"");
        foreach (var extension in ReferenceImporter.Extensions.Append(".acnote"))
        {
            supported.SetValue(extension, "");
            using var openWith = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
            openWith.SetValue("AcNote.Reference", Array.Empty<byte>(), RegistryValueKind.None);
        }
    }
}
