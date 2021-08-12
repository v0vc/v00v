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
            this.FindControl<Button>("OpenFile").Click += async delegate
            {
                var ofd = await new OpenFileDialog { Title = "Select file", Filters = GetFilters() }.ShowAsync(GetWindow());

                if (ofd.Length == 1)
                {
                    AvaloniaLocator.Current.GetService<IStartupModel>().WatchApp = ofd[0];
                }
            };
            this.FindControl<Button>("SelectFolder").Click += async delegate
            {
                var ofd = await new OpenFolderDialog { Title = "Select folder" }.ShowAsync(GetWindow());

                if (!string.IsNullOrWhiteSpace(ofd))
                {
                    AvaloniaLocator.Current.GetService<IStartupModel>().DownloadDir = ofd;
                }
            };
            this.FindControl<Button>("SelectDbFolder").Click += async delegate
            {
                var ofd = await new OpenFolderDialog { Title = "Select folder" }.ShowAsync(GetWindow());

                if (!string.IsNullOrWhiteSpace(ofd))
                {
                    AvaloniaLocator.Current.GetService<IStartupModel>().DbDir = ofd;
                }
            };
        }

        #endregion

        #region Static Methods

        private static List<FileDialogFilter> GetFilters()
        {
            return new() { new FileDialogFilter { Name = "Application (.exe)", Extensions = new List<string> { "exe" } } };
        }

        #endregion

        #region Methods

        private Window GetWindow() => (Window)VisualRoot;

        #endregion
    }
}
