using System.Text;

namespace PaycheckCalculator.App.Helpers;

/// <summary>Turns a user-supplied export label into a file-system-safe base file name.</summary>
internal static class FileNameSanitizer
{
    /// <summary>
    /// Returns a safe base name (no extension) from <paramref name="baseName"/>, replacing
    /// characters that are invalid in file names with hyphens and trimming. Falls back to
    /// <paramref name="fallback"/> when the input is blank or reduces to nothing.
    /// </summary>
    public static string Sanitize(string? baseName, string fallback = "Paycheck-Summary")
    {
        if (string.IsNullOrWhiteSpace(baseName))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(baseName.Length);
        foreach (var ch in baseName.Trim())
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '-' : ch);

        var cleaned = sb.ToString().Trim();
        return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
    }
}
