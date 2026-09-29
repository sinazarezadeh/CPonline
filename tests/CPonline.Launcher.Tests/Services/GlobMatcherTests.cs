using CPonline.Launcher.Core.Services;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("RED4ext-v*-windows.zip", "RED4ext-v1.2.3-windows.zip", true)]
    [InlineData("RED4ext-v*-windows.zip", "red4ext-v1.2.3-windows.zip", true)] // case-insensitive
    [InlineData("RED4ext-v*-windows.zip", "RED4ext-v1.2.3-linux.zip", false)]
    [InlineData("*win64*client*.zip", "cyberpunkmp-win64-client-v0.9.zip", true)]
    [InlineData("*win64*client*.zip", "cyberpunkmp-win64-server-v0.9.zip", false)]
    [InlineData("input-loader-?.zip", "input-loader-1.zip", true)]
    [InlineData("input-loader-?.zip", "input-loader-12.zip", false)]
    [InlineData("exact.zip", "exact.zip", true)]
    [InlineData("exact.zip", "not-exact.zip", false)]
    public void IsMatch_MatchesWildcardsAsExpected(string pattern, string candidate, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(pattern, candidate));
    }

    [Fact]
    public void IsMatch_EscapesRegexMetacharactersInLiteralParts()
    {
        // '.' in the pattern must mean a literal dot, not "any character".
        Assert.False(GlobMatcher.IsMatch("file.zip", "fileXzip"));
        Assert.True(GlobMatcher.IsMatch("file.zip", "file.zip"));
    }
}
