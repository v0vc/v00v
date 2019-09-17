using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using v00v.ViewModel;

namespace v00v.Views.Application
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            this.AttachDevTools();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
