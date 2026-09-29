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

    [Fact]
    public async Task DownloadAsync_ReportsProgressUpToOneWhenContentLengthIsKnown()
    {
        var payload = new byte[200_000]; // bigger than the internal read buffer, so multiple reports fire
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        });
        var client = new GitHubReleaseClient(new HttpClient(handler));

        var updates = new List<double>();
        var progress = new SynchronousProgress<double>(updates.Add);

        using var destination = new MemoryStream();
        await client.DownloadAsync("https://example.invalid/asset.zip", destination, progress);

        Assert.NotEmpty(updates);
        Assert.True(updates.Count > 1, "Expected multiple progress updates for a payload larger than one read buffer.");
        Assert.All(updates, u => Assert.InRange(u, 0.0, 1.0));
        Assert.Equal(1.0, updates[^1]);
        Assert.True(payload.Length == destination.Length, "The full payload should still have been written.");
    }

    [Fact]
    public async Task DownloadAsync_NoContentLength_StillCopiesButReportsNothing()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream([1, 2, 3])),
            };
            response.Content.Headers.ContentLength = null; // simulate a chunked/unknown-length response
            return response;
        });
        var client = new GitHubReleaseClient(new HttpClient(handler));

        var updates = new List<double>();
        var progress = new SynchronousProgress<double>(updates.Add);

        using var destination = new MemoryStream();
        await client.DownloadAsync("https://example.invalid/asset.zip", destination, progress);

        Assert.Empty(updates);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination.ToArray());
    }
}
