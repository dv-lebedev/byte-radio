using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ByteRadio.Broadcast;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly AudioBroadcaster _broadcaster = new();

    public MainWindow()
    {
        InitializeComponent();

        _broadcaster.StatusChanged += OnStatusChanged;
        _broadcaster.ErrorOccurred += OnErrorOccurred;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        ErrorTextBlock.Text = string.Empty;

        try
        {
            await _broadcaster.StartAsync(UrlTextBox.Text);
            StopButton.IsEnabled = true;
            UrlTextBox.IsEnabled = false;
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = ex.Message;
            StatusTextBlock.Text = "Failed to start";
            StartButton.IsEnabled = true;
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;

        try
        {
            await _broadcaster.StopAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = ex.Message;
        }
        finally
        {
            StartButton.IsEnabled = true;
            UrlTextBox.IsEnabled = true;
        }
    }

    private void OnStatusChanged(object? sender, string status)
    {
        Dispatcher.Invoke(() => StatusTextBlock.Text = status);
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        Dispatcher.Invoke(() => ErrorTextBlock.Text = ex.Message);
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _broadcaster.DisposeAsync();
        base.OnClosed(e);
    }
}