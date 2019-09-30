using System;
using AutoMapper;
using Avalonia;
using Avalonia.Logging.Serilog;
using Microsoft.EntityFrameworkCore;
using v00v.MainApp;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Database;
using v00v.Services.Persistence;
using v00v.Services.Persistence.Mappers;
using v00v.Services.Persistence.Repositories;
using v00v.ViewModel.Popup;

namespace v00v
{
    internal static class Program
    {
        #region Static Methods

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToDebug();

        private static void AppShutdown()
        {
            var applog = AvaloniaLocator.Current.GetService<IAppLogRepository>();
            applog.SetStatus(AppStatus.AppClosed, "App closed");
            var context = AvaloniaLocator.Current.GetService<IContextFactory>().CreateVideoContext();
            var closedCount = applog.GetStatusCount(AppStatus.AppClosed).GetAwaiter().GetResult();
            if (closedCount % 10 == 0 && closedCount != 0)
            {
                context.Database.ExecuteSqlCommand($"VACUUM");
            }
            context.Dispose();
            AvaloniaLocator.Current.GetService<IPopupController>().Trigger.Dispose();
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Register();

            PreAppStart(false);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            AppShutdown();
        }

        private static void PreAppStart(bool needMigrate)
        {
            if (needMigrate)
            {
                var db = AvaloniaLocator.Current.GetService<VideoContext>();
                db.Database.Migrate();
            }

            var appLog = AvaloniaLocator.Current.GetService<IAppLogRepository>();
            appLog.AppId = Guid.NewGuid().ToString();
            appLog.SetStatus(AppStatus.AppStarted, "App started");
        }

        private static void Register()
        {
            AvaloniaLocator.CurrentMutable.Bind<IYoutubeService>().ToSingleton<YoutubeService>();

            AvaloniaLocator.CurrentMutable.Bind<IPopupController>().ToSingleton<PopupController>();

            AvaloniaLocator.CurrentMutable.Bind<IContextFactory>().ToSingleton<ContextFactory>();

            AvaloniaLocator.CurrentMutable.Bind<IMapper>().ToConstant(new MapperConfiguration(mc =>
            {
                mc.AddProfile(new ChannelMapProfile());
                mc.AddProfile(new PlaylistMapProfile());
                mc.AddProfile(new ItemMapProfile());
                mc.AddProfile(new TagMapProfile());
            }).CreateMapper());

            AvaloniaLocator.CurrentMutable.Bind<IChannelRepository>()
                .ToConstant(new ChannelRepository(AvaloniaLocator.Current.GetService<IContextFactory>(),
                                                  AvaloniaLocator.Current.GetService<IMapper>()));

            AvaloniaLocator.CurrentMutable.Bind<IItemRepository>()
                .ToConstant(new ItemRepository(AvaloniaLocator.Current.GetService<IContextFactory>(),
                                               AvaloniaLocator.Current.GetService<IMapper>()));

            AvaloniaLocator.CurrentMutable.Bind<IPlaylistRepository>()
                .ToConstant(new PlaylistRepository(AvaloniaLocator.Current.GetService<IContextFactory>(),
                                                   AvaloniaLocator.Current.GetService<IMapper>()));

            AvaloniaLocator.CurrentMutable.Bind<ITagRepository>()
                .ToConstant(new TagRepository(AvaloniaLocator.Current.GetService<IContextFactory>(),
                                              AvaloniaLocator.Current.GetService<IMapper>()));

            AvaloniaLocator.CurrentMutable.Bind<IAppLogRepository>()
                .ToConstant(new AppLogRepository(AvaloniaLocator.Current.GetService<IContextFactory>()));
        }

        #endregion
    }
}
