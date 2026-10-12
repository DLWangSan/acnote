using System.Text;
using System.Security.Cryptography;
using AcNote.Core;
using Microsoft.Win32;

namespace AcNote.App;

public static class WordAddinRegistrar
{
    public static readonly Guid Clsid = new("C4A1E0B7-7E2D-4C5A-9F10-6D2B8A1E0C33");
    public const string ProgId = "AcNote.Connect";

    public static void Unregister()
    {
        using var app = Registry.CurrentUser.OpenSubKey(@"Software\AcNote");
        if (!string.Equals(app?.GetValue("AppPath") as string, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return;
        foreach (var path in new[]
        {
            @"Software\Microsoft\Office\Word\Addins\" + ProgId,
            @"Software\Classes\CLSID\" + Clsid.ToString("B"),
            @"Software\Classes\" + ProgId,
            @"Software\Classes\Applications\AcNote.App.exe",
            @"Software\Classes\AcNote.Reference",
        }) Registry.CurrentUser.DeleteSubKeyTree(path, false);
        using (var registry32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
        {
            foreach (var path in new[]
            {
                @"Software\Microsoft\Office\Word\Addins\" + ProgId,
                @"Software\Classes\CLSID\" + Clsid.ToString("B"),
                @"Software\Classes\" + ProgId,
                @"Software\Microsoft\Office\16.0\Word\Resiliency\DoNotDisableAddinList",
                @"Software\Kingsoft\Office\WPS\AddinsWL",
            })
            {
                if (path.EndsWith("AddinsWL", StringComparison.OrdinalIgnoreCase) || path.EndsWith("DoNotDisableAddinList", StringComparison.OrdinalIgnoreCase))
                {
                    using var key = registry32.OpenSubKey(path, writable: true);
                    key?.DeleteValue(ProgId, false);
                }
                else registry32.DeleteSubKeyTree(path, false);
            }
            using var own32 = registry32.OpenSubKey(@"Software\AcNote", true);
            own32?.DeleteValue("ComHostPath32", false);
        }
        foreach (var extension in AcNote.Import.EndNote.ReferenceImporter.Extensions.Append(".acnote"))
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}\OpenWithProgids", true);
            key?.DeleteValue("AcNote.Reference", false);
        }
        foreach (var path in new[] { @"Software\Kingsoft\Office\WPS\AddinsWL", @"Software\Microsoft\Office\16.0\Word\Resiliency\DoNotDisableAddinList" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, true); key?.DeleteValue(ProgId, false);
        }
        using var own = Registry.CurrentUser.OpenSubKey(@"Software\AcNote", true);
        own?.DeleteValue("AppPath", false); own?.DeleteValue("ComHostPath", false);
    }

    public static string TryRegister(bool force = false)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "addin");
        var comhost = Path.Combine(bundled, "AcNote.WordAddin.comhost.dll");
        var comhost32 = Path.Combine(AppContext.BaseDirectory, "addin-x86", "AcNote.WordAddin.comhost.dll");
        var registeredComhost32 = "";
        var appExe = Environment.ProcessPath ?? "";
        if (!File.Exists(comhost))
            return "未找到 Word 插件，功能区尚未登记。";

        var previousApp = Registry.GetValue(@"HKEY_CURRENT_USER\Software\AcNote", "AppPath", null) as string;
        if (!force && IsDebugBuild(appExe) && !string.IsNullOrWhiteSpace(previousApp) &&
            !IsDebugBuild(previousApp) && File.Exists(previousApp))
        {
            var installedComHost = Registry.GetValue(@"HKEY_CURRENT_USER\Software\AcNote", "ComHostPath", null) as string ?? "";
            var kept = "已保留已安装版 Word 插件注册。请从开始菜单启动 AcNote 使用安装版。";
            WriteRegistrationLog(kept + Environment.NewLine + "AppPath=" + previousApp + Environment.NewLine + "ComHostPath=" + installedComHost);
            return kept;
        }

        try
        {
            // Install each build in its own directory; Word may still hold an older build open.
            var files = Directory.GetFiles(bundled, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToList();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files) hash.AppendData(File.ReadAllBytes(file));
            var version = Convert.ToHexString(hash.GetHashAndReset())[..16];
            var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcNote", "addin", version);
            foreach (var file in files)
            {
                var destination = Path.Combine(installed, Path.GetRelativePath(bundled, file));
                if (File.Exists(destination)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            comhost = Path.Combine(installed, "AcNote.WordAddin.comhost.dll");
            var clsid = Clsid.ToString("B");
            RegisterOfficeView(Registry.CurrentUser, comhost);
            using (var wps = Registry.CurrentUser.CreateSubKey(@"Software\Kingsoft\Office\WPS\AddinsWL"))
                wps?.SetValue(ProgId, "");
            if (File.Exists(comhost32))
            {
                var bundled32 = Path.GetDirectoryName(comhost32)!;
                var files32 = Directory.GetFiles(bundled32, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToList();
                var installed32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcNote", "addin-x86", version);
                foreach (var file in files32)
                {
                    var destination = Path.Combine(installed32, Path.GetRelativePath(bundled32, file));
                    if (File.Exists(destination)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination);
                }
                var comhost32Installed = Path.Combine(installed32, "AcNote.WordAddin.comhost.dll");
                registeredComhost32 = comhost32Installed;
                using var registry32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
                RegisterOfficeView(registry32, comhost32Installed);
                using var wps = registry32.CreateSubKey(@"Software\Kingsoft\Office\WPS\AddinsWL");
                wps?.SetValue(ProgId, "");
                using var app32 = registry32.CreateSubKey(@"Software\AcNote");
                app32?.SetValue("ComHostPath32", comhost32Installed);
            }

            using (var app = Registry.CurrentUser.CreateSubKey(@"Software\AcNote"))
            {
                app?.SetValue("AppPath", appExe);
                app?.SetValue("ComHostPath", comhost);
            }

            VerifyRegistration(appExe, comhost, comhost32);

            ClearStartupMark();
            var success = "Word 功能区已登记。重新打开 Word 后，选项卡显示 AcNote。";
            WriteRegistrationLog(success + Environment.NewLine + "AppPath=" + appExe + Environment.NewLine + "ComHostPath=" + comhost + Environment.NewLine + "ComHostPath32=" + registeredComhost32);
            return success;
        }
        catch (Exception ex)
        {
            var failure = "Word 功能区登记失败：" + ex.Message;
            WriteRegistrationLog(failure + Environment.NewLine + "AppPath=" + appExe + Environment.NewLine + "ComHostPath=" + comhost);
            return failure;
        }
    }

    private static bool IsDebugBuild(string path) => path.Contains(
        Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);

    private static void RegisterOfficeView(RegistryKey root, string comhost)
    {
        var clsid = Clsid.ToString("B");
        WriteDefault(root, $@"Software\Classes\CLSID\{clsid}", "AcNote.Connect");
        using (var key = root.CreateSubKey($@"Software\Classes\CLSID\{clsid}\InprocServer32"))
        {
            key?.SetValue("", comhost);
            key?.SetValue("ThreadingModel", "Apartment");
        }
        WriteDefault(root, $@"Software\Classes\CLSID\{clsid}\ProgID", ProgId);
        WriteDefault(root, $@"Software\Classes\{ProgId}", "AcNote");
        WriteDefault(root, $@"Software\Classes\{ProgId}\CLSID", clsid);
        using (var word = root.CreateSubKey(@"Software\Microsoft\Office\Word\Addins\" + ProgId))
        {
            word?.SetValue("FriendlyName", "AcNote");
            word?.SetValue("Description", "AcNote");
            word?.SetValue("LoadBehavior", 3, RegistryValueKind.DWord);
            word?.SetValue("CommandLineSafe", 0, RegistryValueKind.DWord);
        }
        using (var keep = root.CreateSubKey(@"Software\Microsoft\Office\16.0\Word\Resiliency\DoNotDisableAddinList"))
            keep?.SetValue(ProgId, 1, RegistryValueKind.DWord);
    }

    private static void VerifyRegistration(string appExe, string comhost, string comhost32)
    {
        var registeredApp = Registry.GetValue(@"HKEY_CURRENT_USER\Software\AcNote", "AppPath", null) as string;
        var registeredHost = Registry.GetValue(@"HKEY_CURRENT_USER\Software\AcNote", "ComHostPath", null) as string;
        var clsidPath = $@"HKEY_CURRENT_USER\Software\Classes\CLSID\{Clsid:B}\InprocServer32";
        var clsidHost = Registry.GetValue(clsidPath, null, null) as string;
        using var word = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\Word\Addins\" + ProgId);
        var loadBehavior = word?.GetValue("LoadBehavior");
        if (!string.Equals(registeredApp, appExe, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(registeredHost, comhost, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(clsidHost, comhost, StringComparison.OrdinalIgnoreCase) ||
            loadBehavior is null || Convert.ToInt32(loadBehavior) != 3)
            throw new InvalidOperationException("注册表回读与插件安装路径不一致。请完全退出 AcNote、Word 和 WPS 后重新安装；如果仍失败，请把 word-registration.log 发给开发者。");

        if (File.Exists(comhost32))
        {
            using var registry32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
            var host32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcNote", "addin-x86");
            var host32 = registry32.OpenSubKey(@"Software\AcNote")?.GetValue("ComHostPath32") as string;
            var clsidHost32 = registry32.OpenSubKey($@"Software\Classes\CLSID\{Clsid:B}\InprocServer32")?.GetValue(null) as string;
            using var wps = registry32.OpenSubKey(@"Software\Kingsoft\Office\WPS\AddinsWL");
            if (string.IsNullOrWhiteSpace(host32) || !host32.StartsWith(host32Path, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(clsidHost32, host32, StringComparison.OrdinalIgnoreCase) ||
                wps is null || !wps.GetValueNames().Contains(ProgId, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("32 位 WPS 插件注册未通过回读核验。请重新安装并查看 word-registration.log。");
        }
        using var wps64 = Registry.CurrentUser.OpenSubKey(@"Software\Kingsoft\Office\WPS\AddinsWL");
        if (wps64 is null || !wps64.GetValueNames().Contains(ProgId, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("64 位 WPS 插件白名单注册未通过回读核验。");
    }

    private static void WriteRegistrationLog(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(Path.Combine(AppPaths.Root, "word-registration.log"), DateTime.Now.ToString("s") + Environment.NewLine + message);
        }
        catch { /* Diagnostics must never prevent startup or registration. */ }
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
        WriteDefault(Registry.CurrentUser, path, value);
    }

    private static void WriteDefault(RegistryKey root, string path, string value)
    {
        using var key = root.CreateSubKey(path);
        key?.SetValue("", value);
    }
}
