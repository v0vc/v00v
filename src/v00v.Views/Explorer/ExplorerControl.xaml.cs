using System.Linq;
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

        private void ItemsDoubleTapped(object? sender, RoutedEventArgs e)
        {
            var litem = ((IVisual)e.Source).GetSelfAndVisualAncestors()
                .OfType<ListBoxItem>()
                .FirstOrDefault();

            if (litem?.DataContext is Item item)
            {
                var settings = AvaloniaLocator.Current.GetService<IStartupModel>();
                item.RunItem(settings.WatchApp,
                             settings.DownloadDir,
                             $"{AvaloniaLocator.Current.GetService<IYoutubeService>().ItemLink}{item.Id}");

                if (item.WatchState == WatchState.Watched)
                {
                    return;
                }
                if (this.FindControl<ExplorerControl>("explorer")?.DataContext is ExplorerModel expModel)
                {
                    expModel.SetItemState(WatchState.Watched).GetAwaiter().GetResult();
                }
            }
        }

        #endregion
    }
}
