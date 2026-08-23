namespace ChatUnpack.Core.Domain;

public enum TranscriptStatus
{
  Complete,
  Incomplete,
  Cancelled,
  Failed
}

public static class TranscriptStatusNames
{
  public static string DisplayName(this TranscriptStatus status)
  {
    return status switch
    {
      TranscriptStatus.Complete => "完整",
      TranscriptStatus.Incomplete => "提取未完成",
      TranscriptStatus.Cancelled => "已取消",
      TranscriptStatus.Failed => "失败",
      _ => string.Empty
    };
  }
}

public sealed class Transcript : IEquatable<Transcript>
{
  public Transcript(
    string title,
    DateTimeOffset? extractedAt = null,
    TranscriptStatus status = TranscriptStatus.Incomplete,
    IEnumerable<ChatMessage>? messages = null,
    IEnumerable<ScanWarning>? warnings = null)
  {
    Title = title ?? string.Empty;
    ExtractedAt = extractedAt ?? DateTimeOffset.Now;
    Status = status;
    Messages = messages?.ToList() ?? new List<ChatMessage>();
    Warnings = warnings?.ToList() ?? new List<ScanWarning>();
  }

  public string Title { get; set; }
  public DateTimeOffset ExtractedAt { get; set; }
  public TranscriptStatus Status { get; set; }
  public List<ChatMessage> Messages { get; set; }
  public List<ScanWarning> Warnings { get; set; }

  public bool Equals(Transcript? other)
  {
    return other is not null
      && Title == other.Title
      && ExtractedAt == other.ExtractedAt
      && Status == other.Status
      && Messages.SequenceEqual(other.Messages)
      && Warnings.SequenceEqual(other.Warnings);
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as Transcript);
  }

  public override int GetHashCode()
  {
    var hash = new HashCode();
    hash.Add(Title);
    hash.Add(ExtractedAt);
    hash.Add(Status);
    foreach (var message in Messages)
    {
      hash.Add(message);
    }

    foreach (var warning in Warnings)
    {
      hash.Add(warning);
    }

    return hash.ToHashCode();
  }
}
