using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Application
{
    public class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
