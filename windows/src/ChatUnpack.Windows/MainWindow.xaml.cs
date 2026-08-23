using System.Windows;

namespace ChatUnpack.Windows;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    DataContext = new AppViewModel();
  }
}
