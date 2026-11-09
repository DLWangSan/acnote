using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void ReleaseHubResponseProvidesHostedInstallerAndChineseNotes()
    {
        var release = UpdateService.Parse("""
            {"ok":true,"release":{"version":"0.1.19","download_url":"https://update.ygs.plus/downloads/setup.exe","notes":"新增作者分隔符设置"}}
            """);
        Assert.Equal(UpdateService.ParseVersion("0.1.19"), release!.Version);
        Assert.Equal("https://update.ygs.plus/downloads/setup.exe", release.DownloadUrl);
        Assert.Equal("新增作者分隔符设置", release.Notes);
    }

    [Fact]
    public void LegacyManifestStillWorksAndUnpublishedReleaseIsEmpty()
    {
        Assert.NotNull(UpdateService.Parse("""{"version":"0.1.18","downloadUrl":"https://example.org/setup.exe"}"""));
        Assert.Null(UpdateService.Parse("""{"ok":true,"release":null}"""));
    }

    [Fact]
    public void ReleaseHubRejectsErrorsAndUnsafeDownloadAddresses()
    {
        Assert.Throws<InvalidDataException>(() => UpdateService.Parse("""{"ok":false}"""));
        Assert.Throws<InvalidDataException>(() => UpdateService.Parse("""{"ok":true,"release":{"version":"0.1.19","download_url":"http://example.org/setup.exe"}}"""));
    }
}
