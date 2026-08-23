using System.Text;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.Assembly;

public sealed record OverlapDecision(int OverlapCount, bool IsReliable, bool IsAmbiguous = false);

public sealed class OverlapMatcher
{
  public OverlapMatcher(int maximumOverlapMessages = 32, double fuzzyThreshold = 0.84)
  {
    MaximumOverlapMessages = maximumOverlapMessages;
    FuzzyThreshold = fuzzyThreshold;
  }

  public int MaximumOverlapMessages { get; }
  public double FuzzyThreshold { get; }

  public OverlapDecision Match(
    IReadOnlyList<ChatMessage> previousTail,
    IReadOnlyList<ChatMessage> currentHead)
  {
    if (previousTail.Count == 0 || currentHead.Count == 0)
    {
      return new OverlapDecision(0, true);
    }

    var tail = previousTail
      .Skip(Math.Max(0, previousTail.Count - MaximumOverlapMessages))
      .ToList();
    var head = currentHead
      .Take(MaximumOverlapMessages)
      .ToList();
    var maximumCount = Math.Min(tail.Count, head.Count);

    if (maximumCount > 0)
    {
      for (var count = maximumCount; count >= 1; count--)
      {
        var requiresSenderMatch = count == 1;
        var matches = true;
        for (var index = 0; index < count; index++)
        {
          var lhs = tail[tail.Count - count + index];
          var rhs = head[index];
          if (!ExactMatch(lhs, rhs, requiresSenderMatch))
          {
            matches = false;
            break;
          }
        }

        if (matches)
        {
          return new OverlapDecision(count, true);
        }
      }
    }

    var candidates = new List<FuzzyCandidate>();
    if (maximumCount >= 2)
    {
      for (var count = 2; count <= maximumCount; count++)
      {
        var scores = new List<double>(count);
        for (var index = 0; index < count; index++)
        {
          scores.Add(FuzzyScore(
            tail[tail.Count - count + index],
            head[index]));
        }

        if (scores.All(score => score >= FuzzyThreshold))
        {
          candidates.Add(new FuzzyCandidate(count, scores.Average()));
        }
      }
    }

    if (candidates.Count == 0)
    {
      return new OverlapDecision(0, true);
    }

    var best = candidates
      .OrderByDescending(candidate => candidate.Count)
      .ThenByDescending(candidate => candidate.Score)
      .First();
    return new OverlapDecision(best.Count, true);
  }

  private static bool ExactMatch(ChatMessage lhs, ChatMessage rhs, bool requiresSenderMatch)
  {
    var sender = Normalize(lhs.Sender.Text);
    var timestamp = Normalize(lhs.Timestamp.Text);
    if (requiresSenderMatch
      && (sender.Length == 0
        || timestamp.Length == 0
        || sender != Normalize(rhs.Sender.Text)))
    {
      return false;
    }

    return timestamp == Normalize(rhs.Timestamp.Text)
      && Normalize(BodyText(lhs)) == Normalize(BodyText(rhs))
      && lhs.Kind == rhs.Kind;
  }

  private static double FuzzyScore(ChatMessage lhs, ChatMessage rhs)
  {
    var timestamp = Normalize(lhs.Timestamp.Text);
    if (timestamp.Length == 0 || timestamp != Normalize(rhs.Timestamp.Text))
    {
      return 0;
    }

    var leftBody = Normalize(BodyText(lhs));
    var rightBody = Normalize(BodyText(rhs));
    var bodyScore = leftBody.Length == 0 && rightBody.Length == 0
      ? 1
      : CharacterSimilarity(leftBody, rightBody);
    var kindScore = lhs.Kind == rhs.Kind ? 1.0 : 0.0;
    return bodyScore * 0.75 + kindScore * 0.25;
  }

  private static string BodyText(ChatMessage message)
  {
    return string.Join("\n", message.Body.Select(line => line.Text));
  }

  private static string Normalize(string text)
  {
    return string.Join(
      " ",
      (text ?? string.Empty)
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Replace('　', ' ')
        .Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
      .Trim();
  }

  private static double CharacterSimilarity(string lhs, string rhs)
  {
    if (lhs == rhs)
    {
      return 1;
    }

    var left = lhs.EnumerateRunes().ToArray();
    var right = rhs.EnumerateRunes().ToArray();
    if (left.Length == 0 && right.Length == 0)
    {
      return 1;
    }

    var previous = Enumerable.Range(0, right.Length + 1).ToArray();
    for (var leftIndex = 0; leftIndex < left.Length; leftIndex++)
    {
      var current = new int[right.Length + 1];
      current[0] = leftIndex + 1;
      for (var rightIndex = 0; rightIndex < right.Length; rightIndex++)
      {
        var substitution = previous[rightIndex]
          + (left[leftIndex] == right[rightIndex] ? 0 : 1);
        var insertion = current[rightIndex] + 1;
        var deletion = previous[rightIndex + 1] + 1;
        current[rightIndex + 1] = Math.Min(substitution, Math.Min(insertion, deletion));
      }

      previous = current;
    }

    var distance = previous[right.Length];
    return 1 - (double)distance / Math.Max(left.Length, right.Length);
  }

  private sealed record FuzzyCandidate(int Count, double Score);
}
