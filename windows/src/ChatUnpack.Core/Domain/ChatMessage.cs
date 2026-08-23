namespace ChatUnpack.Core.Domain;

public record struct RecognizedField
{
  public RecognizedField(string text, double? confidence, bool isUserCorrected = false)
  {
    Text = text ?? string.Empty;
    Confidence = confidence;
    IsUserCorrected = isUserCorrected;
  }

  public string Text { get; set; }
  public double? Confidence { get; set; }
  public bool IsUserCorrected { get; set; }

  public bool IsLowConfidence => Confidence is < 0.45 && !IsUserCorrected;
}

public sealed class RecognizedLine : IEquatable<RecognizedLine>
{
  public RecognizedLine(
    string text,
    double? confidence,
    bool isUserCorrected = false,
    Guid? id = null)
  {
    Id = id ?? Guid.NewGuid();
    Text = text ?? string.Empty;
    Confidence = confidence;
    IsUserCorrected = isUserCorrected;
  }

  public Guid Id { get; }
  public string Text { get; set; }
  public double? Confidence { get; set; }
  public bool IsUserCorrected { get; set; }

  public bool IsLowConfidence => Confidence is < 0.45 && !IsUserCorrected;

  public bool Equals(RecognizedLine? other)
  {
    return other is not null
      && Id == other.Id
      && Text == other.Text
      && Confidence == other.Confidence
      && IsUserCorrected == other.IsUserCorrected;
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as RecognizedLine);
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(Id, Text, Confidence, IsUserCorrected);
  }
}

public enum MessageKind
{
  Text,
  Image,
  Voice,
  Video,
  File,
  MiniProgram,
  Link,
  NestedRecord,
  Emoji,
  UnknownNonText
}

public sealed class ChatMessage : IEquatable<ChatMessage>
{
  public ChatMessage(
    RecognizedField sender,
    RecognizedField timestamp,
    IEnumerable<RecognizedLine> body,
    MessageKind kind = MessageKind.Text,
    IEnumerable<ScanWarning>? warnings = null,
    IEnumerable<int>? sourceViewportIndices = null,
    bool isPartial = false,
    Guid? id = null)
  {
    Id = id ?? Guid.NewGuid();
    Sender = sender;
    Timestamp = timestamp;
    Body = body?.ToList() ?? new List<RecognizedLine>();
    Kind = kind;
    Warnings = warnings?.ToList() ?? new List<ScanWarning>();
    SourceViewportIndices = sourceViewportIndices is null
      ? new HashSet<int>()
      : new HashSet<int>(sourceViewportIndices);
    IsPartial = isPartial;
  }

  public Guid Id { get; }
  public RecognizedField Sender { get; set; }
  public RecognizedField Timestamp { get; set; }
  public List<RecognizedLine> Body { get; set; }
  public MessageKind Kind { get; set; }
  public List<ScanWarning> Warnings { get; set; }
  public HashSet<int> SourceViewportIndices { get; set; }
  public bool IsPartial { get; set; }

  public bool HasLowConfidence => string.IsNullOrEmpty(Timestamp.Text);

  public bool Equals(ChatMessage? other)
  {
    return other is not null
      && Id == other.Id
      && Sender == other.Sender
      && Timestamp == other.Timestamp
      && Body.SequenceEqual(other.Body)
      && Kind == other.Kind
      && Warnings.SequenceEqual(other.Warnings)
      && SourceViewportIndices.SetEquals(other.SourceViewportIndices)
      && IsPartial == other.IsPartial;
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as ChatMessage);
  }

  public override int GetHashCode()
  {
    var hash = new HashCode();
    hash.Add(Id);
    hash.Add(Sender);
    hash.Add(Timestamp);
    foreach (var line in Body)
    {
      hash.Add(line);
    }

    hash.Add(Kind);
    foreach (var warning in Warnings)
    {
      hash.Add(warning);
    }

    foreach (var viewportIndex in SourceViewportIndices.OrderBy(value => value))
    {
      hash.Add(viewportIndex);
    }

    hash.Add(IsPartial);
    return hash.ToHashCode();
  }
}
