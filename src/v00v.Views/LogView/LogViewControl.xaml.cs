using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.LogView
{
    public class LogViewControl : UserControl
    {
        #region Constructors

        public LogViewControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
