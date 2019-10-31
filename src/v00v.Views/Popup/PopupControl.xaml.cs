using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using v00v.ViewModel.Popup;

namespace v00v.Views.Popup
{
    public class PopupControl : UserControl
    {
        #region Constructors

        public PopupControl()
        {
            AvaloniaXamlLoader.Load(this);
            DataContextChanged += PopupControl_DataContextChanged;
        }

        #endregion

        #region Event Handling

        private void PopupControl_DataContextChanged(object sender, EventArgs e)
        {
            var context = (sender as PopupControl)?.DataContext as PopupModel;
            if (context?.Context != null)
            {
                Focus();
            }
        }

        #endregion
    }
}
