using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using v00v.Views.Application;

namespace v00v.MainApp
{
    public class App : Application
    {
        #region Methods

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktopLifetime:
                    desktopLifetime.MainWindow = new MainWindow();
                    break;
                case ISingleViewApplicationLifetime singleViewLifetime:
                    singleViewLifetime.MainView = new MainView();
                    break;
            }

            base.OnFrameworkInitializationCompleted();
        }

        #endregion
    }
}
