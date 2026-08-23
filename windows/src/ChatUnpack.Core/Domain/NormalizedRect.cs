namespace ChatUnpack.Core.Domain;

public readonly record struct NormalizedRect
{
  public NormalizedRect(double x, double y, double width, double height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public double X { get; }
  public double Y { get; }
  public double Width { get; }
  public double Height { get; }

  public double Top => 1 - (Y + Height);

  public double CenterY => Top + Height / 2;
}
