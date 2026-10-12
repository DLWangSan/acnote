using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace AcNote.Core;

public sealed record UpdateRelease(Version Version, string DownloadUrl, string Notes);

public static class UpdateService
{
    public const string DefaultSource = "https://api.github.com/repos/DLWangSan/acnote/releases/latest";
    public const string ReleasesPage = "https://github.com/DLWangSan/acnote/releases";
    public static Version ParseVersion(string text)
    {
        var version = text.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(version, out var parsed)) throw new InvalidDataException("更新版本号必须是正式版本，例如 0.1.0。");
        return new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
    }
    public static string HttpsUrl(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("请使用有效的 HTTPS 地址。");
        return uri.AbsoluteUri;
    }
    public static UpdateRelease? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("tag_name", out var tag))
        {
            if ((root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) ||
                (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())) return null;
            var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(a =>
                a.GetProperty("name").GetString() is string name && name.StartsWith("AcNote-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase));
            if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("此版本还没有 Windows 安装包，请稍后重试。");
            return new UpdateRelease(ParseVersion(tag.GetString() ?? ""), HttpsUrl(asset.GetProperty("browser_download_url").GetString() ?? ""),
                root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
        }
        return new UpdateRelease(ParseVersion(root.GetProperty("version").GetString() ?? ""),
            HttpsUrl(root.GetProperty("downloadUrl").GetString() ?? ""), root.TryGetProperty("notes", out var notes) ? notes.GetString() ?? "" : "");
    }
    public static async Task<UpdateRelease?> CheckAsync(string source, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AcNote-UpdateCheck/0.1");
        using var response = await client.GetAsync(HttpsUrl(source), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound && source == DefaultSource) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1_048_576) throw new InvalidDataException("更新信息过大。");
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + read > 1_048_576) throw new InvalidDataException("更新信息过大。");
            buffer.Write(bytes, 0, read);
        }
        return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
