using System.Windows;
using ByteRadio.Broadcast.ViewModels;

namespace ByteRadio.Broadcast;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _viewModel.DisposeAsync();
        base.OnClosed(e);
    }
}