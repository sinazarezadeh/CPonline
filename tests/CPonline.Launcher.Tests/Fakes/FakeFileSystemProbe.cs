using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.Tests.Fakes;

public sealed class FakeFileSystemProbe : IFileSystemProbe
{
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> FileContents { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> DirectoryFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => ExistingFiles.Contains(path);

    public bool DirectoryExists(string path) => ExistingDirectories.Contains(path);

    public string ReadAllText(string path) =>
        FileContents.TryGetValue(path, out var content) ? content : throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern) =>
        DirectoryFiles.TryGetValue(directory, out var files) ? files : Enumerable.Empty<string>();
}
