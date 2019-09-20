using System.Windows.Input;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Core;

namespace v00v.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region Fields

        private int _count;
        private double _position;

        #endregion

        #region Constructors

        public MainWindowViewModel()
        {
            Count = 0;
            Position = 100.0;
            MoveLeftCommand = new Command((param) => Position -= 5.0);
            MoveRightCommand = new Command((param) => Position += 5.0);
            ResetMoveCommand = new Command((param) => Position = 100.0);
            CatalogModel = new CatalogModel();
            PageIndex = 0;
        }

        #endregion

        #region Properties

        public CatalogModel CatalogModel { get; set; }

        public byte PageIndex { get; set; }
        public int Count
        {
            get => _count;
            set => Update(ref _count, value);
        }

        public ICommand MoveLeftCommand { get; set; }

        public ICommand MoveRightCommand { get; set; }

        public double Position
        {
            get => _position;
            set => Update(ref _position, value);
        }

        public ICommand ResetMoveCommand { get; set; }

        #endregion

        #region Methods

        public void DecrementCount(object sender, object parameter) => Count--;

        public void IncrementCount() => Count++;

        #endregion
    }
}
