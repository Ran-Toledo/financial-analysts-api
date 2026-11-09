using System.Globalization;
using System.Text;

namespace FinancialAnalystsAPI.Ingestion;

/// <summary>
/// Turns firm and analyst names into match keys, so "Morgan Stanley &amp; Co." and "MORGAN STANLEY"
/// resolve to the same firm.
/// </summary>
public static class NameNormalizer
{
    private static readonly HashSet<string> LegalSuffixes =
        ["inc", "llc", "ltd", "co", "corp", "corporation", "plc", "lp", "llp", "sa", "ag", "nv", "company"];

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // strip accents
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }

        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (tokens.Count > 1 && LegalSuffixes.Contains(tokens[^1])) tokens.RemoveAt(tokens.Count - 1);
        return string.Join(' ', tokens);
    }
}
