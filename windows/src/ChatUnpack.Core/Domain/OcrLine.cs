namespace ChatUnpack.Core.Domain;

public sealed class OcrLine : IEquatable<OcrLine>
{
  public OcrLine(
    string text,
    double? confidence,
    NormalizedRect boundingBox,
    IEnumerable<string>? alternatives = null,
    int viewportIndex = 0)
  {
    Text = text ?? string.Empty;
    Confidence = confidence;
    BoundingBox = boundingBox;
    ViewportIndex = viewportIndex;
    Alternatives = alternatives?.ToArray() ?? Array.Empty<string>();
  }

  public string Text { get; }
  public double? Confidence { get; }
  public NormalizedRect BoundingBox { get; }
  public IReadOnlyList<string> Alternatives { get; }
  public int ViewportIndex { get; }

  public double Top => BoundingBox.Top;

  public double CenterY => Top + BoundingBox.Height / 2;

  public bool Equals(OcrLine? other)
  {
    if (ReferenceEquals(this, other))
    {
      return true;
    }

    return other is not null
      && Text == other.Text
      && Confidence == other.Confidence
      && BoundingBox == other.BoundingBox
      && Alternatives.SequenceEqual(other.Alternatives)
      && ViewportIndex == other.ViewportIndex;
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as OcrLine);
  }

  public override int GetHashCode()
  {
    var hash = new HashCode();
    hash.Add(Text);
    hash.Add(Confidence);
    hash.Add(BoundingBox);
    hash.Add(ViewportIndex);
    foreach (var alternative in Alternatives)
    {
      hash.Add(alternative);
    }

    return hash.ToHashCode();
  }
}
