using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Application
{
    public class MainView : UserControl
    {
        #region Constructors

        public MainView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
