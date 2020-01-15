using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using v00v.ViewModel;

namespace v00v.Views.Application
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new MainWindowViewModel();
            //this.AttachDevTools();
        }
    }
}
