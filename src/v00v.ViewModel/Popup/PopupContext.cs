using System.Windows.Input;
using v00v.Model;

namespace v00v.ViewModel.Popup
{
    public class PopupContext : ViewModelBase
    {
        #region Fields

        private bool _isWorking;

        #endregion

        #region Properties

        public ICommand CloseCommand { get; set; }

        public bool IsWorking
        {
            get => _isWorking;
            set => Update(ref _isWorking, value);
        }

        public string Title { get; set; }

        #endregion
    }
}
