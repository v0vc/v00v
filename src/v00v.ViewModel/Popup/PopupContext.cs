using Avalonia.Media.Imaging;
using v00v.Model;

namespace v00v.ViewModel.Popup
{
    public class PopupContext : ViewModelBase
    {
        #region Fields

        private bool _canExpanded;
        private int _currentHeight;
        private int _currentWidth;
        private Bitmap _expandThumb;
        private bool _isWorking;
        private string _title;

        #endregion

        #region Properties

        public bool CanExpanded
        {
            get => _canExpanded;
            set => Update(ref _canExpanded, value);
        }

        public int CurrentHeight
        {
            get => _currentHeight;
            set => Update(ref _currentHeight, value);
        }

        public int CurrentWidth
        {
            get => _currentWidth;
            set => Update(ref _currentWidth, value);
        }

        public Bitmap ExpandThumb
        {
            get => _expandThumb;
            set => Update(ref _expandThumb, value);
        }

        public bool IsWorking
        {
            get => _isWorking;
            set => Update(ref _isWorking, value);
        }

        public string Title
        {
            get => _title;
            set => Update(ref _title, value);
        }

        #endregion
    }
}
