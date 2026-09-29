namespace CPonline.Launcher.Core.Services;

/// <summary>Thin seam over file existence checks so orchestration logic (which paths win,
/// in what order) can be unit-tested with a fake instead of touching the real disk.</summary>
public interface IFileSystemProbe
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    string ReadAllText(string path);

    IEnumerable<string> EnumerateFiles(string directory, string searchPattern);
}
