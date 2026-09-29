using CPonline.Launcher.Core.Services;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class ServerAddressParserTests
{
    [Theory]
    [InlineData("203.0.113.5:11778", "203.0.113.5", 11778)]
    [InlineData(" 203.0.113.5:11778 ", "203.0.113.5", 11778)]
    [InlineData("my.server.example.com:11778", "my.server.example.com", 11778)]
    [InlineData("localhost:8080", "localhost", 8080)]
    public void TryParse_ValidAddress_ExtractsHostAndPort(string input, string expectedHost, int expectedPort)
    {
        var ok = ServerAddressParser.TryParse(input, out var host, out var port);

        Assert.True(ok);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-port-here")]
    [InlineData(":11778")]
    [InlineData("203.0.113.5:")]
    [InlineData("203.0.113.5:not-a-number")]
    [InlineData("203.0.113.5:0")]
    [InlineData("203.0.113.5:70000")]
    public void TryParse_InvalidAddress_ReturnsFalse(string? input)
    {
        var ok = ServerAddressParser.TryParse(input, out var host, out var port);

        Assert.False(ok);
        Assert.Equal(string.Empty, host);
        Assert.Equal(0, port);
    }
}
