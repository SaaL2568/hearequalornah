using System.Windows;
using HearEqualOrNah.UI.ViewModels;

namespace HearEqualOrNah.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}