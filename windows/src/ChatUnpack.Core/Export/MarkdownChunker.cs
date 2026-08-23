using System.Globalization;

namespace ChatUnpack.Core.Export;

public sealed class MarkdownChunker
{
  private const int MarkerReserve = 80;

  public MarkdownChunker(int maximumCharacters = 1800)
  {
    if (maximumCharacters < 200)
    {
      throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
    }

    MaximumCharacters = maximumCharacters;
  }

  public int MaximumCharacters { get; }

  public List<string> Split(string markdown)
  {
    var value = markdown ?? string.Empty;
    if (TextElementCount(value) <= MaximumCharacters)
    {
      return [value];
    }

    var payloadLimit = MaximumCharacters - MarkerReserve;
    var payloads = SplitPayload(value, payloadLimit);
    var total = payloads.Count;
    return payloads
      .Select((payload, index) => $"{Marker(index + 1, total)}\n\n{payload}")
      .ToList();
  }

  private List<string> SplitPayload(string markdown, int limit)
  {
    var remaining = markdown;
    var payloads = new List<string>();
    while (TextElementCount(remaining) > limit)
    {
      var prefix = TakeTextElements(remaining, limit);
      var splitIndex = PreferredSplitIndex(prefix, limit / 2)
        ?? prefix.Length;
      payloads.Add(remaining[..splitIndex]);
      remaining = remaining[splitIndex..];
    }

    if (remaining.Length > 0)
    {
      payloads.Add(remaining);
    }

    return payloads;
  }

  private static int? PreferredSplitIndex(string prefix, int minimumOffset)
  {
    var separators = new[] { "\n\n---\n\n", "\n\n", "\n" };
    foreach (var separator in separators)
    {
      var index = prefix.LastIndexOf(separator, StringComparison.Ordinal);
      if (index < 0)
      {
        continue;
      }

      var end = index + separator.Length;
      if (TextElementCount(prefix[..end]) >= minimumOffset)
      {
        return end;
      }
    }

    return null;
  }

  private static string TakeTextElements(string value, int count)
  {
    var starts = StringInfo.ParseCombiningCharacters(value);
    if (starts.Length <= count)
    {
      return value;
    }

    return value[..starts[count]];
  }

  private static int TextElementCount(string value)
  {
    return StringInfo.ParseCombiningCharacters(value).Length;
  }

  private static string Marker(int part, int total)
  {
    return part == total
      ? $"【聊天记录分段 {part}/{total}，已发送完毕，请统一处理全部分段】"
      : $"【聊天记录分段 {part}/{total}，请等待全部 {total} 段发送完成后统一处理】";
  }
}
