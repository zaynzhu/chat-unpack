using System.Windows;

namespace ChatUnpack.Windows;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    DataContext = new AppViewModel();
  }

  private void ImportPanel_DragOver(object sender, DragEventArgs e)
  {
    e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
      ? DragDropEffects.Copy
      : DragDropEffects.None;
    e.Handled = true;
  }

  private void ImportPanel_Drop(object sender, DragEventArgs e)
  {
    if (e.Data.GetData(DataFormats.FileDrop) is string[] paths
      && DataContext is AppViewModel viewModel)
    {
      viewModel.AddImportFiles(paths);
    }

    e.Handled = true;
  }
}
