namespace CPonline.Launcher.Core.Services;

/// <summary>Real filesystem implementation of <see cref="IFileSystemProbe"/>.</summary>
public sealed class FileSystemProbe : IFileSystemProbe
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern) =>
        Directory.Exists(directory) ? Directory.EnumerateFiles(directory, searchPattern) : Enumerable.Empty<string>();
}
