namespace EventPulse.EventService.Models;

/// <summary>
/// EP-36 / US-16: Authoritative definition of supported event categories in EventPulse.
/// Defines the controlled canonical category list and backward-compatible normalization for legacy aliases.
/// </summary>
public static class EventCategories
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
    /// Legacy category values mapped to their canonical equivalents to prevent breaking existing data.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Musical Concert"] = Music,
        ["Theatre / Performance"] = ArtsAndTheatre,
        ["Theatre"] = ArtsAndTheatre,
        ["Arts"] = ArtsAndTheatre,
    };

    /// <summary>
    /// Checks whether the supplied category string matches any supported canonical category or recognized legacy alias.
    /// Rejects null, empty, whitespace, and arbitrary unlisted strings.
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
    /// Returns null if null, empty, whitespace, or not a recognized category/alias.
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
