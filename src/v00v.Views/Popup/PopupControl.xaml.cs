using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Popup
{
    public class PopupControl : UserControl
    {
        #region Constructors

        public PopupControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
