using System.Windows;
using ByteRadio.Broadcast.Views;

namespace ByteRadio.Broadcast;

public partial class MainWindow : Window
{
    public MainWindow(MainView mainView)
    {
        InitializeComponent();
        Content = mainView;
    }
}