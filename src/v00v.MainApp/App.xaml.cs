using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using v00v.Services.Database;
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
            var db = AvaloniaLocator.Current.GetService<IContextFactory>();
            using VideoContext context = db.CreateVideoContext();
            context.Database.Migrate();


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
