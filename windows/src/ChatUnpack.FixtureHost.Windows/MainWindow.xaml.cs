using System.Windows;
using System.Windows.Media;

namespace ChatUnpack.FixtureHost.Windows;

public partial class MainWindow : Window
{
  private bool isDarkMode;

  public MainWindow()
  {
    InitializeComponent();
    Messages = FixtureData.CreateMessages();
    DataContext = this;
  }

  public IReadOnlyList<FixtureMessage> Messages { get; }

  public int MessageCount => Messages.Count;

  private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
  {
    isDarkMode = !isDarkMode;

    if (isDarkMode)
    {
      SetBrush("FixtureWindowBrush", "#FF1E2228");
      SetBrush("FixtureHeaderBrush", "#FF292F38");
      SetBrush("FixtureCardBrush", "#FF2F3742");
      SetBrush("FixtureBorderBrush", "#FF46515F");
      SetBrush("FixtureTextBrush", "#FFF3F5F7");
      SetBrush("FixtureMutedBrush", "#FFB9C2CC");
      SetBrush("FixtureAccentBrush", "#FF8FB9F0");
      ThemeToggleButton.Content = "切换浅色";
      return;
    }

    SetBrush("FixtureWindowBrush", "#FFF5F6F8");
    SetBrush("FixtureHeaderBrush", "#FFFFFFFF");
    SetBrush("FixtureCardBrush", "#FFFFFFFF");
    SetBrush("FixtureBorderBrush", "#FFE0E3E8");
    SetBrush("FixtureTextBrush", "#FF202124");
    SetBrush("FixtureMutedBrush", "#FF5F6368");
    SetBrush("FixtureAccentBrush", "#FF315E9B");
    ThemeToggleButton.Content = "切换深色";
  }

  private void SetBrush(string key, string color)
  {
    Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
  }
}
