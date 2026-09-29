using System.Text.Json;

namespace CPonline.Launcher.Core.Services;

public sealed record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size);

public interface IGitHubReleaseClient
{
    /// <summary>Fetches the release pinned by exact tag (never "latest") and returns the first
    /// asset whose filename matches <paramref name="assetNamePattern"/>, or null if the
    /// release/asset isn't found.</summary>
    Task<GitHubReleaseAsset?> FindReleaseAssetAsync(string repo, string tag, string assetNamePattern, CancellationToken ct = default);

    /// <summary>Downloads to <paramref name="destination"/>, reporting fraction complete
    /// (0.0-1.0) via <paramref name="progress"/> when the server reports a Content-Length.
    /// No progress is reported if the length is unknown - callers should treat "no updates" as
    /// indeterminate rather than stalled.</summary>
    Task DownloadAsync(string url, Stream destination, IProgress<double>? progress = null, CancellationToken ct = default);
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

    public async Task DownloadAsync(string url, Stream destination, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        if (progress is null || totalBytes is not > 0)
        {
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
            return;
        }

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            totalRead += bytesRead;
            progress.Report((double)totalRead / totalBytes.Value);
        }
    }
}
