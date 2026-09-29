using System.IO.Compression;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.Core.Services;

public enum ModInstallState
{
    NotInstalled,
    NeedsUpdate,
    UpToDate,
}

/// <summary>Pure "is this mod correctly installed" logic, factored out of <see cref="ModManager"/>
/// so it can be unit-tested without any I/O.</summary>
public static class ModInstallStateEvaluator
{
    public static ModInstallState Evaluate(ModManifestEntry entry, InstalledModRecord? existing)
    {
        if (existing is null)
        {
            return ModInstallState.NotInstalled;
        }

        var tagMatches = string.Equals(existing.Tag, entry.Tag, StringComparison.Ordinal);
        var hashMatches = string.Equals(existing.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase);
        return tagMatches && hashMatches ? ModInstallState.UpToDate : ModInstallState.NeedsUpdate;
    }
}

public interface IModManager
{
    ModInstallState GetState(ModManifestEntry entry, InstalledModRecord? existing);

    /// <summary>Downloads the pinned asset, verifies its checksum, and extracts it onto
    /// <paramref name="gameRoot"/>, preserving the archive's relative paths - the standard
    /// convention for these mod zips, which already mirror the game's own folder layout
    /// (e.g. "red4ext\...", "bin\...", "archive\..."). Throws if the checksum doesn't match
    /// the pinned manifest entry; nothing is extracted in that case.</summary>
    Task<InstalledModRecord> InstallAsync(ModManifestEntry entry, string gameRoot, CancellationToken ct = default);
}

public sealed class ModManager : IModManager
{
    private readonly IGitHubReleaseClient _releaseClient;

    public ModManager(IGitHubReleaseClient releaseClient) => _releaseClient = releaseClient;

    public ModInstallState GetState(ModManifestEntry entry, InstalledModRecord? existing) =>
        ModInstallStateEvaluator.Evaluate(entry, existing);

    public async Task<InstalledModRecord> InstallAsync(ModManifestEntry entry, string gameRoot, CancellationToken ct = default)
    {
        var asset = await _releaseClient.FindReleaseAssetAsync(entry.Repo, entry.Tag, entry.AssetNamePattern, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No release asset matching '{entry.AssetNamePattern}' found for {entry.Repo}@{entry.Tag}.");

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"cponline-{entry.Id}-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var fileStream = File.Create(tempZipPath))
            {
                await _releaseClient.DownloadAsync(asset.BrowserDownloadUrl, fileStream, ct).ConfigureAwait(false);
            }

            var actualHash = await ChecksumVerifier.ComputeSha256Async(tempZipPath, ct).ConfigureAwait(false);
            if (!ChecksumVerifier.Matches(actualHash, entry.Sha256))
            {
                throw new InvalidOperationException(
                    $"Checksum mismatch for '{entry.Id}': expected {entry.Sha256}, got {actualHash}. Refusing to install.");
            }

            var extractedFiles = ExtractOntoGameRoot(tempZipPath, gameRoot);

            return new InstalledModRecord
            {
                ModId = entry.Id,
                Tag = entry.Tag,
                Sha256 = entry.Sha256,
                InstalledAtUtc = DateTimeOffset.UtcNow,
                Files = extractedFiles,
            };
        }
        finally
        {
            File.Delete(tempZipPath);
        }
    }

    private static List<string> ExtractOntoGameRoot(string zipPath, string gameRoot)
    {
        var normalizedRoot = Path.GetFullPath(gameRoot);
        var extractedFiles = new List<string>();

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var zipEntry in archive.Entries)
        {
            if (string.IsNullOrEmpty(zipEntry.Name))
            {
                continue; // directory entry, nothing to extract
            }

            var destination = Path.GetFullPath(Path.Combine(normalizedRoot, zipEntry.FullName));
            if (!destination.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Archive entry '{zipEntry.FullName}' would extract outside the game root - refusing (zip-slip).");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            zipEntry.ExtractToFile(destination, overwrite: true);
            extractedFiles.Add(destination);
        }

        return extractedFiles;
    }
}
