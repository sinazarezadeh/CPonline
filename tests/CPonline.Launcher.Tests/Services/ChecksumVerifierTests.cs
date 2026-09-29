using System.Text;
using CPonline.Launcher.Core.Services;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class ChecksumVerifierTests
{
    [Fact]
    public async Task ComputeSha256Async_MatchesKnownVector()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "hello world", Encoding.ASCII);

            var hash = await ChecksumVerifier.ComputeSha256Async(tempFile);

            // Known SHA-256 of the ASCII bytes "hello world".
            Assert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("ABCDEF", "abcdef", true)]
    [InlineData("abcdef", "abcdef", true)]
    [InlineData("abcdef", "123456", false)]
    public void Matches_IsCaseInsensitive(string computed, string expected, bool shouldMatch)
    {
        Assert.Equal(shouldMatch, ChecksumVerifier.Matches(computed, expected));
    }
}
