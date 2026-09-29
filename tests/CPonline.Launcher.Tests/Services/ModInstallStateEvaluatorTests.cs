using CPonline.Launcher.Core.Services;
using CPonline.Shared.Contracts;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class ModInstallStateEvaluatorTests
{
    private static ModManifestEntry Entry(string tag, string sha256) => new()
    {
        Id = "red4ext",
        DisplayName = "RED4ext",
        Repo = "WopsS/RED4ext",
        Tag = tag,
        AssetNamePattern = "*.zip",
        Sha256 = sha256,
        InstallTargets = ["red4ext"],
    };

    private static InstalledModRecord Record(string tag, string sha256) => new()
    {
        ModId = "red4ext",
        Tag = tag,
        Sha256 = sha256,
        InstalledAtUtc = DateTimeOffset.UtcNow,
        Files = [],
    };

    [Fact]
    public void Evaluate_NoExistingRecord_IsNotInstalled()
    {
        var state = ModInstallStateEvaluator.Evaluate(Entry("v1.0", "aaaa"), existing: null);
        Assert.Equal(ModInstallState.NotInstalled, state);
    }

    [Fact]
    public void Evaluate_MatchingTagAndHash_IsUpToDate()
    {
        var state = ModInstallStateEvaluator.Evaluate(Entry("v1.0", "aaaa"), Record("v1.0", "AAAA"));
        Assert.Equal(ModInstallState.UpToDate, state); // hash comparison is case-insensitive
    }

    [Fact]
    public void Evaluate_DifferentTag_NeedsUpdate()
    {
        var state = ModInstallStateEvaluator.Evaluate(Entry("v2.0", "aaaa"), Record("v1.0", "aaaa"));
        Assert.Equal(ModInstallState.NeedsUpdate, state);
    }

    [Fact]
    public void Evaluate_DifferentHash_NeedsUpdate()
    {
        var state = ModInstallStateEvaluator.Evaluate(Entry("v1.0", "bbbb"), Record("v1.0", "aaaa"));
        Assert.Equal(ModInstallState.NeedsUpdate, state);
    }
}
