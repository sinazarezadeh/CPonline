using System.IO.Compression;
using System.Security.Cryptography;
using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using CPonline.Shared.Contracts;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class ModManagerTests : IDisposable
{
    private readonly string _gameRoot = Directory.CreateTempSubdirectory("cponline-gameroot-").FullName;

    [Fact]
    public async Task InstallAsync_ValidChecksum_ExtractsPreservingRelativePaths()
    {
        var zipBytes = BuildZip(("red4ext/plugins/Codeware/Codeware.dll", "fake-dll-bytes"));
        var sha256 = ComputeSha256Hex(zipBytes);

        var entry = MakeEntry(sha256, "red4ext/plugins/Codeware");
        var releaseClient = new FakeGitHubReleaseClient
        {
            Asset = new GitHubReleaseAsset("codeware-1.7.0-windows.zip", "https://example.invalid/codeware.zip", zipBytes.Length),
            AssetBytes = zipBytes,
        };

        var manager = new ModManager(releaseClient);
        var record = await manager.InstallAsync(entry, _gameRoot);

        Assert.Equal("codeware", record.ModId);
        Assert.Equal(entry.Tag, record.Tag);
        Assert.Equal(sha256, record.Sha256);

        var expectedFile = Path.Combine(_gameRoot, "red4ext", "plugins", "Codeware", "Codeware.dll");
        Assert.Contains(expectedFile, record.Files);
        Assert.True(File.Exists(expectedFile));
        Assert.Equal("fake-dll-bytes", await File.ReadAllTextAsync(expectedFile));
    }

    [Fact]
    public async Task InstallAsync_ChecksumMismatch_ThrowsAndExtractsNothing()
    {
        var zipBytes = BuildZip(("red4ext/plugins/Codeware/Codeware.dll", "fake-dll-bytes"));

        var entry = MakeEntry("0000000000000000000000000000000000000000000000000000000000000000", "red4ext/plugins/Codeware");
        var releaseClient = new FakeGitHubReleaseClient
        {
            Asset = new GitHubReleaseAsset("codeware-1.7.0-windows.zip", "https://example.invalid/codeware.zip", zipBytes.Length),
            AssetBytes = zipBytes,
        };

        var manager = new ModManager(releaseClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallAsync(entry, _gameRoot));
        Assert.False(Directory.Exists(Path.Combine(_gameRoot, "red4ext")));
    }

    [Fact]
    public async Task InstallAsync_NoMatchingAsset_Throws()
    {
        var entry = MakeEntry("aaaa", "red4ext/plugins/Codeware");
        var releaseClient = new FakeGitHubReleaseClient { Asset = null };
        var manager = new ModManager(releaseClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallAsync(entry, _gameRoot));
    }

    [Fact]
    public async Task InstallAsync_ZipEntryEscapingGameRoot_ThrowsZipSlipError()
    {
        var zipBytes = BuildZip(("../../evil.txt", "pwned"));
        var sha256 = ComputeSha256Hex(zipBytes);
        var entry = MakeEntry(sha256, "red4ext");
        var releaseClient = new FakeGitHubReleaseClient
        {
            Asset = new GitHubReleaseAsset("evil.zip", "https://example.invalid/evil.zip", zipBytes.Length),
            AssetBytes = zipBytes,
        };

        var manager = new ModManager(releaseClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallAsync(entry, _gameRoot));
        Assert.Contains("zip-slip", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ModManifestEntry MakeEntry(string sha256, params string[] installTargets) => new()
    {
        Id = "codeware",
        DisplayName = "Codeware",
        Repo = "psiberx/cp2077-codeware",
        Tag = "v1.7.0",
        AssetNamePattern = "codeware-*-windows.zip",
        Sha256 = sha256,
        InstallTargets = installTargets,
    };

    private static byte[] BuildZip(params (string Path, string Content)[] entries)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var zipEntry = archive.CreateEntry(path);
                using var writer = new StreamWriter(zipEntry.Open());
                writer.Write(content);
            }
        }

        return memoryStream.ToArray();
    }

    private static string ComputeSha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public void Dispose() => Directory.Delete(_gameRoot, recursive: true);
}
