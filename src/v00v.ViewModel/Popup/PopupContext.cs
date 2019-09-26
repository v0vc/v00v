using System.Windows.Input;
using v00v.ViewModel.Core;

namespace v00v.ViewModel.Popup
{
    public class PopupContext : ViewModelBase
    {
        #region Properties

        public ICommand CloseCommand { get; set; }
        public string Title { get; set; }

        #endregion
    }
}
