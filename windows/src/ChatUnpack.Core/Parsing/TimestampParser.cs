using System.Text;
using System.Text.RegularExpressions;

namespace ChatUnpack.Core.Parsing;

public sealed record TimestampMatch(string VisibleText, string Prefix = "");

public static class TimestampParser
{
  private static readonly Regex FullDateExpression = new(
    @"(?:\d{4}[年/-]\d{1,2}[月/-]\d{1,2}(?:日)?[ T]?(?:[01]?\d|2[0-3]):[0-5]\d)",
    RegexOptions.CultureInvariant | RegexOptions.Compiled);

  private static readonly Regex RelativeDateExpression = new(
    @"(?:(?:今天|昨天|前天|星期[一二三四五六日天])\s*)?(?:[01]?\d|2[0-3]):[0-5]\d",
    RegexOptions.CultureInvariant | RegexOptions.Compiled);

  public static TimestampMatch? Match(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    if (trimmed.Length == 0 || trimmed.EnumerateRunes().Count() > 60)
    {
      return null;
    }

    var fullDateMatch = FullDateExpression.Match(trimmed);
    if (fullDateMatch.Success && EndsAtTextBoundary(fullDateMatch, trimmed))
    {
      return MakeMatch(fullDateMatch, trimmed);
    }

    var relativeDateMatch = RelativeDateExpression.Match(trimmed);
    if (relativeDateMatch.Success && EndsAtTextBoundary(relativeDateMatch, trimmed))
    {
      return MakeMatch(relativeDateMatch, trimmed);
    }

    return null;
  }

  private static TimestampMatch MakeMatch(Match match, string text)
  {
    var visibleText = text.Substring(match.Index, match.Length).Trim();
    var prefix = text[..match.Index].Trim();
    return new TimestampMatch(visibleText, prefix);
  }

  private static bool EndsAtTextBoundary(Match match, string text)
  {
    return match.Index + match.Length == text.Length;
  }
}
