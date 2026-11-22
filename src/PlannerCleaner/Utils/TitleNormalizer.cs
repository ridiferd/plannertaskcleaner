using System.Text.RegularExpressions;

namespace PlannerCleaner.Utils;

public static partial class TitleNormalizer
{
    /// <summary>Kunci pembanding judul: trim, spasi ganda dirapikan, huruf kecil.</summary>
    public static string Normalize(string? title) =>
        MultiSpace().Replace((title ?? string.Empty).Trim(), " ").ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
