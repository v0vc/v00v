﻿using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.ViewModel.Explorer;
using v00v.ViewModel.Startup;

namespace v00v.Views.Explorer
{
    public class ExplorerControl : UserControl
    {
        #region Constructors

        public ExplorerControl()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<ListBox>("ItemList").AddHandler(Gestures.DoubleTappedEvent, ItemsDoubleTapped);
        }

        #endregion

        #region Event Handling

        private void ItemsDoubleTapped(object sender, RoutedEventArgs e)
        {
            var listBoxItem = ((IVisual)e.Source).GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();

            if (listBoxItem?.DataContext is not Item item)
            {
                return;
            }

            var settings = AvaloniaLocator.Current.GetService<IStartupModel>();
            if (settings != null)
                item.RunItem(settings.WatchApp,
                             settings.DownloadDir,
                             $"{AvaloniaLocator.Current.GetService<IYoutubeService>()?.ItemLink}{item.Id}");

            if (item.WatchState == WatchState.Watched)
            {
                return;
            }

            if (this.FindControl<ExplorerControl>("explorer")?.DataContext is ExplorerModel expModel)
            {
                expModel.SetItemState(WatchState.Watched);
            }
        }

        #endregion
    }
}
