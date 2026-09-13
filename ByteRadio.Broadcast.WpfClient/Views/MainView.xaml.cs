using ByteRadio.Broadcast.WpfClient.ViewModels;
using System.Windows.Controls;

namespace ByteRadio.Broadcast.WpfClient.Views
{
    public partial class MainView : UserControl
    {
        private readonly MainWindowViewModel _viewModel;

        public MainView(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
    }
}