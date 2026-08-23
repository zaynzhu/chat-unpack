namespace ChatUnpack.Core.Domain;

public readonly record struct ScrollPosition
{
  public ScrollPosition(double value, double minimum, double maximum)
  {
    Value = value;
    Minimum = minimum;
    Maximum = maximum;
  }

  public double Value { get; }
  public double Minimum { get; }
  public double Maximum { get; }

  public double Normalized
  {
    get
    {
      if (!IsUsable)
      {
        return 0;
      }

      return Math.Min(1, Math.Max(0, (Value - Minimum) / (Maximum - Minimum)));
    }
  }

  public bool IsUsable
  {
    get
    {
      return double.IsFinite(Value)
        && double.IsFinite(Minimum)
        && double.IsFinite(Maximum)
        && Maximum - Minimum > 0.0001
        && Value >= Minimum - 0.01
        && Value <= Maximum + 0.01;
    }
  }

  public bool IsAtBottom => IsUsable && Normalized >= 0.99;
}
