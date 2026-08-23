using System.Text;
using System.Text.RegularExpressions;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.Parsing;

public sealed class MessageParser
{
  public sealed class Configuration
  {
    public Configuration(double timeBandTolerance = 0.03, double partialEdgeTolerance = 0.025)
    {
      TimeBandTolerance = timeBandTolerance;
      PartialEdgeTolerance = partialEdgeTolerance;
    }

    public double TimeBandTolerance { get; set; }
    public double PartialEdgeTolerance { get; set; }
  }

  private static readonly char[] SenderPunctuation = ['。', '！', '？', '!', '?', '；', ';', '\n'];
  private readonly Configuration configuration;

  public MessageParser(Configuration? configuration = null)
  {
    this.configuration = configuration ?? new Configuration();
  }

  public List<ChatMessage> Parse(IEnumerable<OcrLine> lines, int viewportIndex)
  {
    var orderedLines = lines?.ToList() ?? new List<OcrLine>();
    orderedLines.Sort(CompareLines);

    var headerIndices = orderedLines
      .Select((line, index) => new { Line = line, Index = index })
      .Where(entry => TimestampParser.Match(entry.Line.Text) is not null)
      .Select(entry => entry.Index)
      .ToList();

    if (headerIndices.Count == 0)
    {
      if (orderedLines.Count == 0)
      {
        return new List<ChatMessage>();
      }

      var body = orderedLines
        .Select(line => new RecognizedLine(line.Text, line.Confidence))
        .ToList();
      return new List<ChatMessage>
      {
        new(
          new RecognizedField(string.Empty, null),
          new RecognizedField(string.Empty, null),
          body,
          warnings: [ScanWarning.MissingTimestampAnchor()],
          sourceViewportIndices: [viewportIndex],
          isPartial: true)
      };
    }

    var headers = headerIndices
      .Select(headerIndex =>
      {
        var headerLine = orderedLines[headerIndex];
        var match = TimestampParser.Match(headerLine.Text);
        var rawPrefix = match?.Prefix ?? string.Empty;
        var senderPrefix = IsPlausibleSender(rawPrefix) ? rawPrefix : string.Empty;
        var sender = SenderCandidateFor(
          headerLine,
          headerIndex,
          orderedLines,
          senderPrefix);
        return new Header(
          headerIndex,
          match,
          sender,
          rawPrefix.Length == 0 || senderPrefix.Length > 0 ? null : rawPrefix,
          Math.Min(headerIndex, sender?.LineIndex ?? headerIndex));
      })
      .ToList();

    var messages = new List<ChatMessage>(headers.Count);
    for (var position = 0; position < headers.Count; position++)
    {
      var header = headers[position];
      var headerIndex = header.LineIndex;
      var headerLine = orderedLines[headerIndex];
      var timestampText = header.Match?.VisibleText ?? headerLine.Text;
      var nextBlockStartIndex = position + 1 < headers.Count
        ? headers[position + 1].BlockStartIndex
        : orderedLines.Count;

      var senderText = header.Sender?.Line.Text ?? string.Empty;
      var senderConfidence = header.Sender?.Line.Confidence;
      var bodyStartIndex = Math.Min(headerIndex + 1, orderedLines.Count);
      var bodyEndIndex = Math.Max(bodyStartIndex, Math.Min(nextBlockStartIndex, orderedLines.Count));
      var bodyOcrLines = orderedLines
        .Skip(bodyStartIndex)
        .Take(bodyEndIndex - bodyStartIndex)
        .Where((_, offset) => headerIndex + 1 + offset != header.Sender?.LineIndex)
        .ToList();
      var bodyLines = bodyOcrLines
        .Select(line => new RecognizedLine(line.Text, line.Confidence))
        .ToList();

      if (header.RejectedPrefix is not null)
      {
        bodyLines.Insert(0, new RecognizedLine(header.RejectedPrefix, headerLine.Confidence));
      }
      else if (IsLikelyVisualNoise(bodyOcrLines))
      {
        bodyLines.Clear();
      }

      var kind = Classify(string.Join("\n", bodyLines.Select(line => line.Text)));
      var warnings = new List<ScanWarning>();
      if (timestampText.Length == 0)
      {
        warnings.Add(ScanWarning.MissingTimestampAnchor());
      }

      var firstTop = headerLine.Top;
      var lastBottom = firstTop;
      var blockStartIndex = Math.Max(0, Math.Min(header.BlockStartIndex, orderedLines.Count));
      var blockEndIndex = Math.Max(blockStartIndex, Math.Min(nextBlockStartIndex, orderedLines.Count));
      for (var index = blockStartIndex; index < blockEndIndex; index++)
      {
        lastBottom = Math.Max(
          lastBottom,
          orderedLines[index].Top + orderedLines[index].BoundingBox.Height);
      }

      var isPartial = firstTop <= configuration.PartialEdgeTolerance
        || lastBottom >= 1 - configuration.PartialEdgeTolerance;
      messages.Add(new ChatMessage(
        new RecognizedField(senderText, senderConfidence),
        new RecognizedField(timestampText, headerLine.Confidence),
        bodyLines,
        kind,
        warnings,
        [viewportIndex],
        isPartial));
    }

    return messages;
  }

  private static int CompareLines(OcrLine lhs, OcrLine rhs)
  {
    if (Math.Abs(lhs.Top - rhs.Top) > 0.002)
    {
      return lhs.Top.CompareTo(rhs.Top);
    }

    return lhs.BoundingBox.X.CompareTo(rhs.BoundingBox.X);
  }

  private SenderCandidate? SenderCandidateFor(
    OcrLine timestampLine,
    int headerIndex,
    IReadOnlyList<OcrLine> lines,
    string prefix)
  {
    if (prefix.Length > 0)
    {
      return new SenderCandidate(
        headerIndex,
        new OcrLine(
          prefix,
          timestampLine.Confidence,
          timestampLine.BoundingBox,
          timestampLine.Alternatives,
          timestampLine.ViewportIndex));
    }

    SenderCandidate? best = null;
    var bestMaxX = double.MinValue;
    for (var index = 0; index < lines.Count; index++)
    {
      if (index == headerIndex)
      {
        continue;
      }

      var line = lines[index];
      var heightBasedTolerance = Math.Max(
        line.BoundingBox.Height,
        timestampLine.BoundingBox.Height) * 0.6;
      var tolerance = Math.Min(configuration.TimeBandTolerance, heightBasedTolerance);
      if (Math.Abs(line.CenterY - timestampLine.CenterY) > tolerance)
      {
        continue;
      }

      var lineMaxX = line.BoundingBox.X + line.BoundingBox.Width;
      var timestampMinX = timestampLine.BoundingBox.X;
      if (lineMaxX > timestampMinX + 0.02)
      {
        continue;
      }

      if (TimestampParser.Match(line.Text) is not null)
      {
        continue;
      }

      var senderLine = SenderLineFrom(line);
      if (senderLine is null || lineMaxX < bestMaxX)
      {
        continue;
      }

      best = new SenderCandidate(index, senderLine);
      bestMaxX = lineMaxX;
    }

    return best;
  }

  private static bool IsPlausibleSender(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    if (trimmed.Length == 0 || CountRunes(trimmed) > 32)
    {
      return false;
    }

    if (trimmed.IndexOfAny(SenderPunctuation) >= 0)
    {
      return false;
    }

    if (IsDateLike(trimmed))
    {
      return false;
    }

    return trimmed.EnumerateRunes().Any(Rune.IsLetterOrDigit);
  }

  private static OcrLine? SenderLineFrom(OcrLine line)
  {
    var candidates = new[] { line.Text }.Concat(line.Alternatives);
    var text = candidates.FirstOrDefault(IsPlausibleSender);
    if (text is null)
    {
      return null;
    }

    if (text == line.Text)
    {
      return line;
    }

    return new OcrLine(
      text,
      line.Confidence,
      line.BoundingBox,
      line.Alternatives,
      line.ViewportIndex);
  }

  private static bool IsDateLike(string text)
  {
    var numberRuns = Regex.Split(text, @"[^\p{N}]+")
      .Where(value => value.Length > 0)
      .ToArray();
    if (numberRuns.Length < 3
      || CountRunes(numberRuns[0]) != 4
      || !int.TryParse(numberRuns[0], out var year)
      || year < 1900
      || year > 2100)
    {
      return false;
    }

    return true;
  }

  private static bool IsLikelyVisualNoise(IReadOnlyList<OcrLine> lines)
  {
    if (lines.Count != 1)
    {
      return false;
    }

    var line = lines[0];
    var trimmed = line.Text.Trim();
    if (trimmed.Length == 0
      || CountRunes(trimmed) > 8
      || line.Confidence is not < 0.65)
    {
      return false;
    }

    var hasHan = trimmed.EnumerateRunes().Any(IsIdeographic);
    var hasLatin = trimmed.EnumerateRunes().Any(IsLatin);
    var hasDigit = trimmed.EnumerateRunes().Any(rune => rune.Value is >= '0' and <= '9');
    return hasHan && hasLatin && hasDigit;
  }

  private static MessageKind Classify(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    return trimmed switch
    {
      "[图片]" or "图片" => MessageKind.Image,
      "[语音]" or "语音" => MessageKind.Voice,
      "[视频]" or "视频" => MessageKind.Video,
      "[文件]" or "文件" => MessageKind.File,
      "[聊天记录]" or "聊天记录" => MessageKind.NestedRecord,
      "[小程序]" or "小程序" => MessageKind.MiniProgram,
      "[链接]" or "链接" => MessageKind.Link,
      "[表情]" or "表情" => MessageKind.Emoji,
      _ => trimmed.Length == 0 ? MessageKind.UnknownNonText : MessageKind.Text
    };
  }

  private static bool IsIdeographic(Rune rune)
  {
    return rune.Value is >= 0x3400 and <= 0x4DBF
      or >= 0x4E00 and <= 0x9FFF
      or >= 0xF900 and <= 0xFAFF
      or >= 0x20000 and <= 0x2FA1F;
  }

  private static bool IsLatin(Rune rune)
  {
    return rune.Value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
  }

  private static int CountRunes(string text)
  {
    return text.EnumerateRunes().Count();
  }

  private sealed record SenderCandidate(int LineIndex, OcrLine Line);

  private sealed record Header(
    int LineIndex,
    TimestampMatch? Match,
    SenderCandidate? Sender,
    string? RejectedPrefix,
    int BlockStartIndex);
}
