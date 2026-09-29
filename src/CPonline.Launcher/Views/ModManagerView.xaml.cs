using System.Windows;
using System.Windows.Controls;
using CPonline.Launcher.ViewModels;
using Microsoft.Win32;

namespace CPonline.Launcher.Views;

public partial class ModManagerView : UserControl
{
    public ModManagerView()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModManagerViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select your Cyberpunk 2077 install folder" };
        if (dialog.ShowDialog() == true)
        {
            viewModel.TrySetManualGameRoot(dialog.FolderName);
        }
    }
}
