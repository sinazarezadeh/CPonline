namespace CPonline.Launcher.Core.Steam;

/// <summary>
/// A single node in Valve's KeyValues ("VDF") text format, e.g. Steam's
/// libraryfolders.vdf. A node either holds a scalar string value or a list of
/// named child nodes (VDF allows repeated keys, hence a list rather than a dictionary).
/// </summary>
public sealed class VdfNode
{
    public string? Value { get; init; }

    public List<(string Key, VdfNode Node)> Children { get; } = new();

    public bool IsLeaf => Value is not null;

    /// <summary>First child with the given key (case-insensitive), or null.</summary>
    public VdfNode? this[string key] =>
        Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).Node;

    public IEnumerable<VdfNode> AllChildren(string key) =>
        Children.Where(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).Select(c => c.Node);
}
