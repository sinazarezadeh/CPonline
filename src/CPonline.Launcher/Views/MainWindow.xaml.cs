using System.Windows;
using CPonline.Launcher.ViewModels;

namespace CPonline.Launcher.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
