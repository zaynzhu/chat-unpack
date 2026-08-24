using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.OCR;

// 把 Windows.Media.Ocr 的像素坐标（左上原点）转成 Core 的归一化坐标。
// Core 的 NormalizedRect.Y 从底部测量，Top = 1-(Y+Height) 从顶部测量。
// imgW/imgH 必须是 OCR 实际看到的（裁剪后消息区）位图像素尺寸。
public static class OcrCoordinateConverter
{
  public static NormalizedRect ToNormalized(
    double pixelX,
    double pixelY,
    double pixelWidth,
    double pixelHeight,
    int imageWidth,
    int imageHeight)
  {
    if (imageWidth <= 0 || imageHeight <= 0)
    {
      throw new ArgumentOutOfRangeException(
        imageWidth <= 0 ? nameof(imageWidth) : nameof(imageHeight),
        "图像像素尺寸必须为正数");
    }

    var x = pixelX / imageWidth;
    var y = 1 - (pixelY + pixelHeight) / imageHeight;
    var width = pixelWidth / imageWidth;
    var height = pixelHeight / imageHeight;
    return new NormalizedRect(x, y, width, height);
  }
}