using System.Security.Cryptography;

namespace CPonline.Launcher.Core.Services;

/// <summary>Computes and compares SHA-256 checksums for downloaded mod archives, verified
/// against the pinned <c>manifests/mods.json</c> hash before anything is ever extracted.</summary>
public static class ChecksumVerifier
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Matches(string computedHex, string expectedHex) =>
        string.Equals(computedHex, expectedHex, StringComparison.OrdinalIgnoreCase);
}
