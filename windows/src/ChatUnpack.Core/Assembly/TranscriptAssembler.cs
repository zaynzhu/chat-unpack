using System.Text;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.Assembly;

public sealed class TranscriptAssembler
{
  private readonly OverlapMatcher overlapMatcher;

  public TranscriptAssembler(
    string title,
    DateTimeOffset? extractedAt = null,
    OverlapMatcher? overlapMatcher = null)
  {
    Transcript = new Transcript(title, extractedAt);
    this.overlapMatcher = overlapMatcher ?? new OverlapMatcher();
  }

  public Transcript Transcript { get; }

  public int MessageCount => Transcript.Messages.Count;

  public int LowConfidenceCount => Transcript.Messages.Count(message =>
    message.HasLowConfidence || message.Warnings.Count > 0);

  public void Append(IEnumerable<ChatMessage> messages, int viewportIndex)
  {
    var currentMessages = (messages ?? Array.Empty<ChatMessage>())
      .Select(message => CloneMessage(message, viewportIndex))
      .ToList();
    if (currentMessages.Count == 0)
    {
      return;
    }

    if (Transcript.Messages.Count == 0)
    {
      Transcript.Messages.AddRange(currentMessages);
      return;
    }

    var first = currentMessages[0];
    var last = Transcript.Messages[^1];
    if (first.IsPartial || last.IsPartial)
    {
      if (SameBoundaryIdentity(last, first) || SameUnanchoredFragment(last, first))
      {
        Transcript.Messages[^1] = Merge(last, first);
        currentMessages.RemoveAt(0);
        if (currentMessages.Count == 0)
        {
          return;
        }
      }
    }

    var decision = overlapMatcher.Match(
      Transcript.Messages
        .TakeLast(overlapMatcher.MaximumOverlapMessages)
        .ToList(),
      currentMessages
        .Take(overlapMatcher.MaximumOverlapMessages)
        .ToList());

    if (decision.IsAmbiguous)
    {
      var warning = ScanWarning.UncertainAssembly();
      Transcript.Warnings.Add(warning);
      currentMessages[0].Warnings.Add(warning);
    }
    else if (decision.OverlapCount > 0)
    {
      var removeCount = Math.Min(decision.OverlapCount, currentMessages.Count);
      currentMessages.RemoveRange(0, removeCount);
    }

    if (currentMessages.Count > 0)
    {
      Transcript.Messages.AddRange(currentMessages);
    }
  }

  public void Finish(TranscriptStatus status, string? reason = null)
  {
    CanonicalizeSenders();
    TrimTrailingHeaderArtifacts();
    Transcript.Status = status;
    if (!string.IsNullOrEmpty(reason))
    {
      Transcript.Warnings.Add(new ScanWarning("CU-STATE", reason));
    }
  }

  public void MarkIncomplete(string reason)
  {
    Finish(TranscriptStatus.Incomplete, reason);
  }

  private static bool SameBoundaryIdentity(ChatMessage lhs, ChatMessage rhs)
  {
    var senderMatches = !string.IsNullOrWhiteSpace(lhs.Sender.Text)
      && lhs.Sender.Text.Trim() == rhs.Sender.Text.Trim();
    var timestampMatches = !string.IsNullOrWhiteSpace(lhs.Timestamp.Text)
      && lhs.Timestamp.Text.Trim() == rhs.Timestamp.Text.Trim();
    return senderMatches
      && timestampMatches
      && lhs.Kind == rhs.Kind
      && BodyOverlapCount(lhs.Body, rhs.Body) > 0;
  }

  private void CanonicalizeSenders()
  {
    var entries = Transcript.Messages
      .Select((message, index) =>
      {
        var text = message.Sender.Text.Trim();
        var core = LongestHanRun(text);
        return new SenderEntry(index, text, core);
      })
      .Where(entry => entry.Text.Length > 0
        && !Transcript.Messages[entry.Index].Sender.IsUserCorrected
        && entry.Core is not null
        && CountRunes(entry.Core) >= 2)
      .ToList();

    var formsByCore = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    foreach (var entry in entries)
    {
      if (!formsByCore.TryGetValue(entry.Core!, out var forms))
      {
        forms = new HashSet<string>(StringComparer.Ordinal);
        formsByCore[entry.Core!] = forms;
      }

      forms.Add(entry.Text);
    }

    var anchors = formsByCore
      .Where(pair => pair.Value.Count >= 4)
      .Select(pair => pair.Key)
      .ToList();
    if (anchors.Count == 0)
    {
      return;
    }

    foreach (var entry in entries)
    {
      var matchingAnchor = anchors
        .Where(anchor => entry.Core!.Contains(anchor, StringComparison.Ordinal)
          && CountRunes(entry.Core) - CountRunes(anchor) <= 1)
        .OrderByDescending(CountRunes)
        .FirstOrDefault();
      if (matchingAnchor is not null)
      {
        Transcript.Messages[entry.Index].Sender = new RecognizedField(
          matchingAnchor,
          Transcript.Messages[entry.Index].Sender.Confidence,
          Transcript.Messages[entry.Index].Sender.IsUserCorrected);
      }
    }
  }

  private static string? LongestHanRun(string text)
  {
    var longest = string.Empty;
    var current = new StringBuilder();
    foreach (var rune in (text ?? string.Empty).EnumerateRunes())
    {
      if (IsIdeographic(rune))
      {
        current.Append(rune.ToString());
        continue;
      }

      if (CountRunes(current.ToString()) > CountRunes(longest))
      {
        longest = current.ToString();
      }

      current.Clear();
    }

    if (CountRunes(current.ToString()) > CountRunes(longest))
    {
      longest = current.ToString();
    }

    return longest.Length == 0 ? null : longest;
  }

  private void TrimTrailingHeaderArtifacts()
  {
    var knownSenders = new HashSet<string>(StringComparer.Ordinal);
    foreach (var message in Transcript.Messages)
    {
      var text = message.Sender.Text.Trim();
      if (text.Length == 0)
      {
        continue;
      }

      knownSenders.Add(text);
      var core = LongestHanRun(text);
      if (core is not null && CountRunes(core) >= 2)
      {
        knownSenders.Add(core);
      }
    }

    if (knownSenders.Count == 0)
    {
      return;
    }

    foreach (var message in Transcript.Messages)
    {
      if (message.Body.Count < 2)
      {
        continue;
      }

      var searchStart = Math.Max(0, message.Body.Count - 4);
      for (var markerIndex = searchStart; markerIndex < message.Body.Count - 1; markerIndex++)
      {
        if (!IsSymbolOnly(message.Body[markerIndex].Text))
        {
          continue;
        }

        var suffix = message.Body.Skip(markerIndex + 1).ToList();
        if (suffix.Count > 3 || !suffix.All(line => IsCompactHeaderText(line.Text)))
        {
          continue;
        }

        var matchesKnown = suffix.Any(line => MatchesKnownSender(line.Text, knownSenders));
        var isSingleNoisySender = suffix.Count == 1
          && IsLikelyNoisySenderArtifact(suffix[0]);
        if (!matchesKnown && !isSingleNoisySender)
        {
          continue;
        }

        message.Body.RemoveRange(markerIndex, message.Body.Count - markerIndex);
        if (message.Body.Count == 0 && message.Kind == MessageKind.Text)
        {
          message.Kind = MessageKind.UnknownNonText;
        }

        break;
      }
    }
  }

  private static bool IsSymbolOnly(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    return trimmed.Length > 0
      && CountRunes(trimmed) <= 3
      && !trimmed.EnumerateRunes().Any(Rune.IsLetterOrDigit);
  }

  private static bool IsCompactHeaderText(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    return trimmed.Length > 0
      && CountRunes(trimmed) <= 16
      && !trimmed.EnumerateRunes().Any(Rune.IsWhiteSpace)
      && trimmed.IndexOfAny(['。', '！', '？', '!', '?', '；', ';']) < 0;
  }

  private static bool IsLikelyNoisySenderArtifact(RecognizedLine line)
  {
    var trimmed = line.Text.Trim();
    if (line.Confidence is not < 0.65
      || CountRunes(trimmed) is < 5 or > 16)
    {
      return false;
    }

    var latinCount = trimmed.EnumerateRunes().Count(IsLatin);
    var hasHan = trimmed.EnumerateRunes().Any(IsIdeographic);
    return latinCount >= 4 && hasHan;
  }

  private static bool MatchesKnownSender(string text, HashSet<string> knownSenders)
  {
    var trimmed = (text ?? string.Empty).Trim();
    if (knownSenders.Contains(trimmed))
    {
      return true;
    }

    var core = LongestHanRun(trimmed);
    return core is not null
      && CountRunes(core) >= 2
      && knownSenders.Contains(core);
  }

  private static bool SameUnanchoredFragment(ChatMessage lhs, ChatMessage rhs)
  {
    if (lhs.Sender.Text.Length > 0
      || rhs.Sender.Text.Length > 0
      || lhs.Timestamp.Text.Length > 0
      || rhs.Timestamp.Text.Length > 0)
    {
      return false;
    }

    var overlapCount = BodyOverlapCount(lhs.Body, rhs.Body);
    if (overlapCount >= 2)
    {
      return true;
    }

    if (overlapCount != 1 || rhs.Body.Count == 0)
    {
      return false;
    }

    return CountRunes(Normalize(rhs.Body[0].Text)) >= 12;
  }

  private static ChatMessage Merge(ChatMessage lhs, ChatMessage rhs)
  {
    var merged = CloneMessage(lhs);
    if ((merged.Sender.Text.Length == 0 || merged.Sender.IsLowConfidence)
      && rhs.Sender.Text.Length > 0)
    {
      merged.Sender = rhs.Sender;
    }

    if ((merged.Timestamp.Text.Length == 0 || merged.Timestamp.IsLowConfidence)
      && rhs.Timestamp.Text.Length > 0)
    {
      merged.Timestamp = rhs.Timestamp;
    }

    merged.Body = MergeBody(merged.Body, rhs.Body);
    foreach (var warning in rhs.Warnings)
    {
      if (!merged.Warnings.Any(existing => existing.Code == warning.Code
        && existing.Message == warning.Message))
      {
        merged.Warnings.Add(warning);
      }
    }

    merged.SourceViewportIndices.UnionWith(rhs.SourceViewportIndices);
    merged.IsPartial = false;
    return merged;
  }

  private static List<RecognizedLine> MergeBody(
    IReadOnlyList<RecognizedLine> lhs,
    IReadOnlyList<RecognizedLine> rhs)
  {
    if (lhs.Count == 0)
    {
      return rhs.ToList();
    }

    if (rhs.Count == 0)
    {
      return lhs.ToList();
    }

    var overlapCount = BodyOverlapCount(lhs, rhs);
    if (overlapCount > 0)
    {
      return lhs.Concat(rhs.Skip(overlapCount)).ToList();
    }

    var leftText = lhs.Select(line => Normalize(line.Text)).ToList();
    var rightText = rhs.Select(line => Normalize(line.Text)).ToList();
    return ContainsSequence(leftText, rightText)
      ? lhs.ToList()
      : lhs.Concat(rhs).ToList();
  }

  private static int BodyOverlapCount(
    IReadOnlyList<RecognizedLine> lhs,
    IReadOnlyList<RecognizedLine> rhs)
  {
    var leftText = lhs.Select(line => Normalize(line.Text)).ToList();
    var rightText = rhs.Select(line => Normalize(line.Text)).ToList();
    var maximumOverlap = Math.Min(leftText.Count, rightText.Count);
    for (var count = maximumOverlap; count >= 1; count--)
    {
      if (leftText.Skip(leftText.Count - count).Take(count).SequenceEqual(rightText.Take(count)))
      {
        return count;
      }
    }

    return 0;
  }

  private static bool ContainsSequence(IReadOnlyList<string> values, IReadOnlyList<string> sequence)
  {
    if (sequence.Count == 0 || sequence.Count > values.Count)
    {
      return false;
    }

    for (var start = 0; start <= values.Count - sequence.Count; start++)
    {
      if (values.Skip(start).Take(sequence.Count).SequenceEqual(sequence))
      {
        return true;
      }
    }

    return false;
  }

  private static string Normalize(string text)
  {
    return (text ?? string.Empty)
      .Replace("\r\n", "\n", StringComparison.Ordinal)
      .Replace('\r', '\n')
      .Trim();
  }

  private static ChatMessage CloneMessage(ChatMessage message, int? viewportIndex = null)
  {
    var sourceViewportIndices = new HashSet<int>(message.SourceViewportIndices);
    if (viewportIndex is not null)
    {
      sourceViewportIndices.Add(viewportIndex.Value);
    }

    return new ChatMessage(
      message.Sender,
      message.Timestamp,
      message.Body.Select(line => new RecognizedLine(
        line.Text,
        line.Confidence,
        line.IsUserCorrected,
        line.Id)),
      message.Kind,
      message.Warnings,
      sourceViewportIndices,
      message.IsPartial,
      message.Id);
  }

  private static bool IsIdeographic(System.Text.Rune rune)
  {
    return rune.Value is >= 0x3400 and <= 0x4DBF
      or >= 0x4E00 and <= 0x9FFF
      or >= 0xF900 and <= 0xFAFF
      or >= 0x20000 and <= 0x2FA1F;
  }

  private static bool IsLatin(System.Text.Rune rune)
  {
    return rune.Value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
  }

  private static int CountRunes(string text)
  {
    return text.EnumerateRunes().Count();
  }

  private sealed record SenderEntry(int Index, string Text, string? Core);
}
