using CPonline.Launcher.Core.Steam;
using Xunit;

namespace CPonline.Launcher.Tests.Steam;

public class SteamLibraryFoldersTests
{
    private const string SampleVdf = """
        "libraryfolders"
        {
            "0"
            {
                "path"        "C:\\Program Files (x86)\\Steam"
                "label"       ""
                "apps"
                {
                    "228980"        "500000"
                }
            }
            "1"
            {
                "path"        "D:\\SteamLibrary"
                "label"       ""
                "apps"
                {
                    "1091500"        "105000000"
                    "440"            "999"
                }
            }
        }
        """;

    [Fact]
    public void GetLibraryPaths_ReturnsEveryLibrary()
    {
        var paths = SteamLibraryFolders.GetLibraryPaths(SampleVdf);

        Assert.Equal(2, paths.Count);
        Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
        Assert.Contains(@"D:\SteamLibrary", paths);
    }

    [Fact]
    public void GetLibraryPathsContainingApp_OnlyReturnsLibrariesThatHaveTheApp()
    {
        var paths = SteamLibraryFolders.GetLibraryPathsContainingApp(SampleVdf, "1091500");

        var path = Assert.Single(paths);
        Assert.Equal(@"D:\SteamLibrary", path);
    }

    [Fact]
    public void GetLibraryPathsContainingApp_UnknownAppId_ReturnsEmpty()
    {
        var paths = SteamLibraryFolders.GetLibraryPathsContainingApp(SampleVdf, "999999999");

        Assert.Empty(paths);
    }

    [Fact]
    public void GetLibraryPaths_MissingLibraryFoldersKey_ReturnsEmpty()
    {
        var paths = SteamLibraryFolders.GetLibraryPaths("\"somethingelse\" { }");

        Assert.Empty(paths);
    }
}
