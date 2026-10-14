namespace EventPulse.BookingService.Models;

/// <summary>
/// EP-393 / EP-394: Canonical categories and legacy alias normalization for report filtering.
/// Consistent with EventService EventCategories definitions.
/// </summary>
public static class ReportCategories
{
    public const string Music = "Music";
    public const string Sports = "Sports";
    public const string Conference = "Conference";
    public const string Workshop = "Workshop";
    public const string Festival = "Festival";
    public const string ArtsAndTheatre = "Arts & Theatre";
    public const string Community = "Community";
    public const string Other = "Other";

    /// <summary>
    /// The canonical controlled list of all supported event categories.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Music,
        Sports,
        Conference,
        Workshop,
        Festival,
        ArtsAndTheatre,
        Community,
        Other,
    };

    private static readonly HashSet<string> CanonicalSet = new(All, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Legacy category values mapped to their canonical equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Musical Concert"] = Music,
        ["Theatre / Performance"] = ArtsAndTheatre,
        ["Theatre"] = ArtsAndTheatre,
        ["Arts"] = ArtsAndTheatre,
    };

    /// <summary>
    /// Checks whether the supplied category matches a supported canonical category or recognized legacy alias.
    /// </summary>
    public static bool IsValid(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        var trimmed = category.Trim();
        return CanonicalSet.Contains(trimmed) || LegacyAliases.ContainsKey(trimmed);
    }

    /// <summary>
    /// Normalizes the supplied category string to its canonical casing and representation.
    /// Automatically converts legacy aliases (e.g. "Musical Concert" -> "Music").
    /// </summary>
    public static string? Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var trimmed = category.Trim();
        if (LegacyAliases.TryGetValue(trimmed, out var canonicalFromAlias))
            return canonicalFromAlias;

        return All.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
