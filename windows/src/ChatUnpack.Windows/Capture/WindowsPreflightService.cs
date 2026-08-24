using System.Security.Principal;
using Windows.Media.Ocr;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 运行条件检查（对应计划 5.1 WindowsPreflightService）。
// 不申请系统权限，不联网下载语言包；缺简体中文 OCR 时停止并给出本地设置指引。
public sealed class WindowsPreflightService
{
  public const string FixtureModeEnvironmentVariable = "CHATUNPACK_FIXTURE_MODE";
  public const int MinimumWindowsBuild = 22631; // Windows 11 23H2

  public PreflightResult Check()
  {
    var winOk = IsWindowsVersionSupported();
    var x64 = IntPtr.Size == 8;
    var d3dOk = D3D11Native.TryCreateDevice();
    var zhOcr = IsChineseOcrAvailable();
    var nonElevated = !IsRunningAsAdministrator();
    var fixture = IsFixtureMode();

    string? code = null;
    string? message = null;
    if (!winOk)
    {
      code = "CUW-P001";
      message = $"系统版本低于 Windows 11 23H2（build {MinimumWindowsBuild}）";
    }
    else if (!zhOcr)
    {
      code = "CUW-P002";
      message = "未安装简体中文 OCR 语言；请在系统设置 → 时间和语言 → 语言 中添加“中文(简体)”并勾选 OCR";
    }
    else if (!d3dOk)
    {
      code = "CUW-P003";
      message = "无法创建 D3D11 设备，图形捕获不可用";
    }
    else if (!x64)
    {
      code = "CUW-P004";
      message = "当前进程不是 x64";
    }
    else if (!nonElevated)
    {
      code = "CUW-P005";
      message = "不应以管理员权限运行；第一版不需要提升权限";
    }

    return new PreflightResult(winOk, x64, d3dOk, zhOcr, nonElevated, fixture, code, message);
  }

  public static bool IsFixtureMode()
  {
#if DEBUG
    var value = Environment.GetEnvironmentVariable(FixtureModeEnvironmentVariable);
    return string.Equals(value, "1", StringComparison.Ordinal);
#else
    return false;
#endif
  }

  private static bool IsWindowsVersionSupported()
  {
    return Environment.OSVersion.Version.Build >= MinimumWindowsBuild;
  }

  private static bool IsChineseOcrAvailable()
  {
    try
    {
      foreach (var language in OcrEngine.AvailableRecognizerLanguages)
      {
        if (language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }
    catch
    {
      return false;
    }
  }

  private static bool IsRunningAsAdministrator()
  {
    try
    {
      using var identity = WindowsIdentity.GetCurrent();
      var principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
      return false;
    }
  }
}