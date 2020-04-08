using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Catalog
{
    public class CatalogControl : UserControl
    {
        #region Constructors

        public CatalogControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
