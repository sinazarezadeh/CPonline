using CPonline.Launcher.Core.Steam;
using Xunit;

namespace CPonline.Launcher.Tests.Steam;

public class VdfParserTests
{
    [Fact]
    public void Parse_ReadsNestedBlocksAndScalarValues()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"    "C:\\Program Files (x86)\\Steam"
                }
            }
            """;

        var root = VdfParser.Parse(vdf);
        var libraryFolders = root["libraryfolders"];

        Assert.NotNull(libraryFolders);
        var zero = libraryFolders!["0"];
        Assert.NotNull(zero);
        Assert.Equal(@"C:\Program Files (x86)\Steam", zero!["path"]!.Value);
    }

    [Fact]
    public void Parse_HandlesEscapedQuotesAndBackslashes()
    {
        const string vdf = """"
            "key"    "a \"quoted\" value with \\backslash"
            """";

        var root = VdfParser.Parse(vdf);
        Assert.Equal("a \"quoted\" value with \\backslash", root["key"]!.Value);
    }

    [Fact]
    public void Parse_SkipsLineComments()
    {
        const string vdf = """
            // this is a comment
            "key" "value" // trailing comment
            """;

        var root = VdfParser.Parse(vdf);
        Assert.Equal("value", root["key"]!.Value);
    }

    [Fact]
    public void Parse_AllowsRepeatedKeysAsSeparateChildren()
    {
        const string vdf = """
            "apps"
            {
                "1091500"    "105000000"
                "1091500"    "999"
            }
            """;

        var root = VdfParser.Parse(vdf);
        var apps = root["apps"]!;
        Assert.Equal(2, apps.AllChildren("1091500").Count());
    }

    [Fact]
    public void Parse_UnterminatedBlock_ThrowsFormatException()
    {
        const string vdf = """
            "libraryfolders"
            {
                "path" "C:\\Steam"
            """;

        Assert.Throws<FormatException>(() => VdfParser.Parse(vdf));
    }

    [Fact]
    public void Parse_UnterminatedQuotedString_ThrowsFormatException()
    {
        const string vdf = "\"key\"    \"unterminated";

        Assert.Throws<FormatException>(() => VdfParser.Parse(vdf));
    }
}
