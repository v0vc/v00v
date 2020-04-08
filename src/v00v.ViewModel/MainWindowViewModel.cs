using System;
using Avalonia;
using v00v.Model;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Startup;

namespace v00v.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly IPopupController _popupController;

        #endregion

        #region Fields

        private byte _pageIndex;
        private PopupModel _popupModel;
        private string _windowTitle;

        #endregion

        #region Constructors

        public MainWindowViewModel() : this(AvaloniaLocator.Current.GetService<IPopupController>())
        {
            PopupModel = new PopupModel(null);
            _popupController?.Trigger?.Subscribe(context =>
            {
                PopupModel = new PopupModel(context);
            });

            CatalogModel = new CatalogModel(SetTitle, SetPageIndex);
            StartupModel = AvaloniaLocator.Current.GetService<IStartupModel>() as StartupModel;
            WindowTitle = $"Channels: {CatalogModel.Entries.Count - 1}";
        }

        private MainWindowViewModel(IPopupController popupController)
        {
            _popupController = popupController;
        }

        #endregion

        #region Properties

        public CatalogModel CatalogModel { get; }

        public byte PageIndex
        {
            get => _pageIndex;
            set => Update(ref _pageIndex, value);
        }

        public PopupModel PopupModel
        {
            get => _popupModel;
            set => Update(ref _popupModel, value);
        }

        public StartupModel StartupModel { get; }

        public string WindowTitle
        {
            get => _windowTitle;
            set => Update(ref _windowTitle, value);
        }

        #endregion

        #region Methods

        private void SetPageIndex(byte index)
        {
            if (PageIndex != index)
            {
                PageIndex = index;
            }
        }

        private void SetTitle(string title)
        {
            WindowTitle = title;
        }

        #endregion
    }
}
