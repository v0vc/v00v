using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Startup
{
    public class StartupControl : UserControl
    {
        #region Constructors

        public StartupControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
