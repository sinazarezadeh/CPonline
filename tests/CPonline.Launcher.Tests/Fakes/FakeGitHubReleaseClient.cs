using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.Tests.Fakes;

public sealed class FakeGitHubReleaseClient : IGitHubReleaseClient
{
    public GitHubReleaseAsset? Asset { get; set; }

    public byte[] AssetBytes { get; set; } = [];

    public Task<GitHubReleaseAsset?> FindReleaseAssetAsync(string repo, string tag, string assetNamePattern, CancellationToken ct = default) =>
        Task.FromResult(Asset);

    public async Task DownloadAsync(string url, Stream destination, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await destination.WriteAsync(AssetBytes, ct);
        progress?.Report(1.0);
    }
}
