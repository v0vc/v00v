using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Explorer
{
    public class ExplorerControl : UserControl
    {
        #region Constructors

        public ExplorerControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
