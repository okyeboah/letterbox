using System.Text.RegularExpressions;

namespace Letterbox.Otp;

public static partial class OtpExtractor
{
    const int KeywordWindow = 40;

    static readonly string[] Keywords = ["code", "otp", "pin"];

    [GeneratedRegex(@"(?<!\d)\d{4,8}(?!\d)")]
    private static partial Regex DigitRuns();

    public static List<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return DigitRuns().Matches(text)
            .Select(match => (value: match.Value, nearKeyword: IsNearKeyword(text, match.Index), position: match.Index))
            .OrderBy(candidate => candidate.nearKeyword ? 0 : 1)
            .ThenBy(candidate => candidate.position)
            .Select(candidate => candidate.value)
            .Distinct()
            .Take(5)
            .ToList();
    }

    static bool IsNearKeyword(string text, int index)
    {
        var prefix = text[Math.Max(0, index - KeywordWindow)..index].ToLowerInvariant();
        return Keywords.Any(prefix.Contains);
    }
}
