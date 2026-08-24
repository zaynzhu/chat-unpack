using ChatUnpack.Core.OCR;

namespace ChatUnpack.Core.TestRunner;

internal static class OcrCoordinateConverterTests
{
  public static void Run(TestSuite suite)
  {
    // 基本转换：像素 (100,200,50,20) + imgW1000/imgH800
    var rect = OcrCoordinateConverter.ToNormalized(100, 200, 50, 20, 1000, 800);
    suite.Expect(Approx(rect.X, 0.1), "X 应为像素 X 除以图像宽");
    suite.Expect(Approx(rect.Y, 0.725), "Y 应为 1-(pY+pH)/imgH");
    suite.Expect(Approx(rect.Width, 0.05), "Width 应为像素宽除以图像宽");
    suite.Expect(Approx(rect.Height, 0.025), "Height 应为像素高除以图像高");
    suite.Expect(Approx(rect.Top, 0.25), "Top 应为 pY/imgH");
    suite.Expect(Approx(rect.CenterY, 0.2625), "CenterY 应为 Top+Height/2");

    // 顶部：像素 Y=0 → Top≈0
    var top = OcrCoordinateConverter.ToNormalized(0, 0, 100, 20, 1000, 800);
    suite.Expect(Approx(top.Top, 0), "像素 Y=0 时 Top 应为 0（顶部）");

    // 底部：像素矩形贴近底部 → Top 接近 1
    var bottom = OcrCoordinateConverter.ToNormalized(0, 780, 100, 20, 1000, 800);
    suite.Expect(Approx(bottom.Top, 0.975), "像素矩形贴近底部时 Top 接近 1");

    // 零像素尺寸
    var zero = OcrCoordinateConverter.ToNormalized(500, 400, 0, 0, 1000, 800);
    suite.Expect(zero.Width == 0 && zero.Height == 0, "零像素尺寸应得零归一化尺寸");

    // X 居中
    var center = OcrCoordinateConverter.ToNormalized(500, 0, 100, 20, 1000, 800);
    suite.Expect(Approx(center.X, 0.5), "X 居中应为 0.5");

    // 图像尺寸非正应抛异常
    var threwZeroWidth = false;
    try
    {
      OcrCoordinateConverter.ToNormalized(0, 0, 10, 10, 0, 800);
    }
    catch (ArgumentOutOfRangeException)
    {
      threwZeroWidth = true;
    }
    suite.Expect(threwZeroWidth, "图像宽为 0 应抛 ArgumentOutOfRangeException");

    var threwZeroHeight = false;
    try
    {
      OcrCoordinateConverter.ToNormalized(0, 0, 10, 10, 1000, 0);
    }
    catch (ArgumentOutOfRangeException)
    {
      threwZeroHeight = true;
    }
    suite.Expect(threwZeroHeight, "图像高为 0 应抛 ArgumentOutOfRangeException");
  }

  private static bool Approx(double value, double expected, double tolerance = 1e-9)
  {
    return Math.Abs(value - expected) < tolerance;
  }
}