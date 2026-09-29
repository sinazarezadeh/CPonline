using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class GitHubReleaseClientTests
{
    private const string ReleaseJson = """
        {
          "tag_name": "v1.7.0",
          "assets": [
            { "name": "codeware-1.7.0-linux.zip", "browser_download_url": "https://example.invalid/codeware-linux.zip", "size": 111 },
            { "name": "codeware-1.7.0-windows.zip", "browser_download_url": "https://example.invalid/codeware-windows.zip", "size": 222 }
          ]
        }
        """;

    [Fact]
    public async Task FindReleaseAssetAsync_ReturnsFirstAssetMatchingPattern()
    {
        var handler = StubHttpMessageHandler.ReturningJson(ReleaseJson);
        var client = new GitHubReleaseClient(new HttpClient(handler));

        var asset = await client.FindReleaseAssetAsync("psiberx/cp2077-codeware", "v1.7.0", "codeware-*-windows.zip");

        Assert.NotNull(asset);
        Assert.Equal("codeware-1.7.0-windows.zip", asset!.Name);
        Assert.Equal("https://example.invalid/codeware-windows.zip", asset.BrowserDownloadUrl);
        Assert.Equal(222, asset.Size);
    }

    [Fact]
    public async Task FindReleaseAssetAsync_NoAssetMatches_ReturnsNull()
    {
        var handler = StubHttpMessageHandler.ReturningJson(ReleaseJson);
        var client = new GitHubReleaseClient(new HttpClient(handler));

        var asset = await client.FindReleaseAssetAsync("psiberx/cp2077-codeware", "v1.7.0", "*-macos.zip");

        Assert.Null(asset);
    }

    [Fact]
    public async Task FindReleaseAssetAsync_SetsUserAgentHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(ReleaseJson) };
        });
        var client = new GitHubReleaseClient(new HttpClient(handler));

        await client.FindReleaseAssetAsync("psiberx/cp2077-codeware", "v1.7.0", "*.zip");

        Assert.NotNull(capturedRequest);
        Assert.NotEmpty(capturedRequest!.Headers.UserAgent);
    }

    [Fact]
    public async Task DownloadAsync_CopiesResponseBodyToDestinationStream()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4]),
        });
        var client = new GitHubReleaseClient(new HttpClient(handler));

        using var destination = new MemoryStream();
        await client.DownloadAsync("https://example.invalid/asset.zip", destination);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, destination.ToArray());
    }
}
