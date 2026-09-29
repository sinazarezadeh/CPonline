using System.Text;
using System.Text.RegularExpressions;

namespace CPonline.Launcher.Core.Services;

/// <summary>Minimal glob matcher (`*` and `?` wildcards only) for picking the right GitHub
/// release asset off a release by filename pattern, e.g. "RED4ext-v*-windows.zip".</summary>
public static class GlobMatcher
{
    public static bool IsMatch(string pattern, string candidate) =>
        BuildRegex(pattern).IsMatch(candidate);

    private static Regex BuildRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*':
                    sb.Append(".*");
                    break;
                case '?':
                    sb.Append('.');
                    break;
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
