using System.Windows;
using ByteRadio.Broadcast.WpfClient.Views;

namespace ByteRadio.Broadcast.WpfClient;

public partial class MainWindow : Window
{
    public MainWindow(MainView mainView)
    {
        InitializeComponent();
        Content = mainView;
    }
}