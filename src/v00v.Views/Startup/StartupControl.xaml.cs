using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using v00v.ViewModel.Startup;

namespace v00v.Views.Startup
{
    public class StartupControl : UserControl
    {
        #region Constructors

        public StartupControl()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<Button>("OpenFile").Click += delegate
            {
                var ofd = new OpenFileDialog { Title = "Select file", Filters = GetFilters() }.ShowAsync(GetWindow());
                ofd.ContinueWith(x =>
                {
                    if (x.IsCompletedSuccessfully && ofd.Result.Length == 1)
                    {
                        AvaloniaLocator.Current.GetService<IStartupModel>().WatchApp = ofd.Result[0];
                    }
                });
            };
            this.FindControl<Button>("SelectFolder").Click += delegate
            {
                var ofd = new OpenFolderDialog { Title = "Select folder" }.ShowAsync(GetWindow());
                ofd.ContinueWith(x =>
                {
                    if (x.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(ofd.Result))
                    {
                        AvaloniaLocator.Current.GetService<IStartupModel>().DownloadDir = ofd.Result;
                    }
                });
            };
            this.FindControl<Button>("SelectDbFolder").Click += delegate
            {
                var ofd = new OpenFolderDialog { Title = "Select folder" }.ShowAsync(GetWindow());
                ofd.ContinueWith(x =>
                {
                    if (x.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(ofd.Result))
                    {
                        AvaloniaLocator.Current.GetService<IStartupModel>().DbDir = ofd.Result;
                    }
                });
            };
        }

        #endregion

        #region Static Methods

        private static List<FileDialogFilter> GetFilters()
        {
            return new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Application (.exe)", Extensions = new List<string> { "exe" } }
            };
        }

        #endregion

        #region Methods

        private Window GetWindow() => (Window)VisualRoot;

        #endregion
    }
}
