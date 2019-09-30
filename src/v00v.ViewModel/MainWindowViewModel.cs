using System;
using Avalonia;
using v00v.Model.Core;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Popup;

namespace v00v.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly IPopupController _popupController;

        #endregion

        #region Fields

        private PopupModel _popupModel;

        #endregion

        #region Constructors

        public MainWindowViewModel() : this(AvaloniaLocator.Current.GetService<IPopupController>())
        {
            PopupModel = PopupModel.Hidden();

            var trigger = _popupController?.Trigger;

            trigger?.Subscribe(context =>
            {
                PopupModel = context == null ? PopupModel.Hidden() : PopupModel.NoHidden(context, _popupController);
            });

            CatalogModel = new CatalogModel();
        }

        private MainWindowViewModel(IPopupController popupController)
        {
            _popupController = popupController;
        }

        #endregion

        #region Properties

        public CatalogModel CatalogModel { get; set; }
        public byte PageIndex { get; set; }

        public PopupModel PopupModel
        {
            get => _popupModel;
            set => Update(ref _popupModel, value);
        }

        #endregion
    }
}
