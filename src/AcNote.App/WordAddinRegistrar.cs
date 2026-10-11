using System.Text;
using Microsoft.Win32;

namespace AcNote.App;

public static class WordAddinRegistrar
{
    public static readonly Guid Clsid = new("C4A1E0B7-7E2D-4C5A-9F10-6D2B8A1E0C33");
    public const string ProgId = "AcNote.Connect";

    public static string TryRegister()
    {
        var comhost = Path.Combine(AppContext.BaseDirectory, "addin", "AcNote.WordAddin.comhost.dll");
        var appExe = Environment.ProcessPath ?? "";
        if (!File.Exists(comhost))
            return "未找到 Word 插件，功能区尚未登记。";

        try
        {
            var clsid = Clsid.ToString("B");
            WriteDefault($@"Software\Classes\CLSID\{clsid}", "AcNote.Connect");
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{clsid}\InprocServer32"))
            {
                key?.SetValue("", comhost);
                key?.SetValue("ThreadingModel", "Apartment");
            }
            WriteDefault($@"Software\Classes\CLSID\{clsid}\ProgID", ProgId);
            WriteDefault($@"Software\Classes\{ProgId}", "AcNote");
            WriteDefault($@"Software\Classes\{ProgId}\CLSID", clsid);

            using (var word = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Office\Word\Addins\" + ProgId))
            {
                word?.SetValue("FriendlyName", "AcNote");
                word?.SetValue("Description", "AcNote");
                word?.SetValue("LoadBehavior", 3, RegistryValueKind.DWord);
                word?.SetValue("CommandLineSafe", 0, RegistryValueKind.DWord);
            }

            using (var keep = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Office\16.0\Word\Resiliency\DoNotDisableAddinList"))
                keep?.SetValue(ProgId, 1, RegistryValueKind.DWord);

            using (var wps = Registry.CurrentUser.CreateSubKey(@"Software\Kingsoft\Office\WPS\AddinsWL"))
                wps?.SetValue(ProgId, "");

            using (var app = Registry.CurrentUser.CreateSubKey(@"Software\AcNote"))
            {
                app?.SetValue("AppPath", appExe);
                app?.SetValue("ComHostPath", comhost);
            }

            ClearStartupMark();
            return "Word 功能区已登记。重新打开 Word 后，选项卡显示 AcNote。";
        }
        catch (Exception ex)
        {
            return "Word 功能区登记失败：" + ex.Message;
        }
    }

    private static void ClearStartupMark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\16.0\Word\Resiliency\StartupItems", writable: true);
        if (key is null)
            return;

        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is byte[] bytes && Encoding.Unicode.GetString(bytes).Contains("acnote.connect", StringComparison.OrdinalIgnoreCase))
                key.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    private static void WriteDefault(string path, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key?.SetValue("", value);
    }
}
