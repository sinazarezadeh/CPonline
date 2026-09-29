using System.Text.Json;

namespace CPonline.Launcher.Core.Services;

public sealed record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size);

public interface IGitHubReleaseClient
{
    /// <summary>Fetches the release pinned by exact tag (never "latest") and returns the first
    /// asset whose filename matches <paramref name="assetNamePattern"/>, or null if the
    /// release/asset isn't found.</summary>
    Task<GitHubReleaseAsset?> FindReleaseAssetAsync(string repo, string tag, string assetNamePattern, CancellationToken ct = default);

    Task DownloadAsync(string url, Stream destination, CancellationToken ct = default);
}

/// <summary>Thin wrapper over the public, unauthenticated GitHub REST API. Fine at this scale
/// (60 requests/hour/IP) since mod versions are pinned and only re-checked on explicit user action.</summary>
public sealed class GitHubReleaseClient : IGitHubReleaseClient
{
    private readonly HttpClient _http;

    public GitHubReleaseClient(HttpClient http)
    {
        _http = http;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("CPonline-Launcher/1.0 (+https://github.com/sinazarezadeh/CPonline)");
        }
    }

    public async Task<GitHubReleaseAsset?> FindReleaseAssetAsync(string repo, string tag, string assetNamePattern, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{repo}/releases/tags/{Uri.EscapeDataString(tag)}";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (name is null || !GlobMatcher.IsMatch(assetNamePattern, name))
            {
                continue;
            }

            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (downloadUrl is null)
            {
                continue;
            }

            var size = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0L;
            return new GitHubReleaseAsset(name, downloadUrl, size);
        }

        return null;
    }

    public async Task DownloadAsync(string url, Stream destination, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await source.CopyToAsync(destination, ct).ConfigureAwait(false);
    }
}
