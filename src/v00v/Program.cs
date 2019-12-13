using System;
using System.IO;
using AutoMapper;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using v00v.MainApp;
using v00v.Model.Enums;
using v00v.Services.Backup;
using v00v.Services.ContentProvider;
using v00v.Services.Database;
using v00v.Services.Dispatcher;
using v00v.Services.Persistence;
using v00v.Services.Persistence.Mappers;
using v00v.Services.Persistence.Repositories;
using v00v.Services.Synchronization;
using v00v.ViewModel.Popup;

namespace v00v
{
    internal static class Program
    {
        #region Static Methods

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI(); //.LogToDebug();

        [STAThread]
        private static void Main(string[] args)
        {
            Register();

            PreAppStart(true);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            Shutdown();

            //try
            //{
            //    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            //}
            //catch (Exception e)
            //{
            //    AvaloniaLocator.Current.GetService<IAppLogRepository>().SetStatus(AppStatus.ExceptionFired, e.Message);
            //}
            //finally
            //{
            //    Shutdown();
            //}
        }

        private static void PreAppStart(bool needMigrate)
        {
            if (needMigrate)
            {
                var db = AvaloniaLocator.Current.GetService<IContextFactory>();
                using (var context = db.CreateVideoContext())
                {
                    context.Database.Migrate();
                }
            }

            var appLog = AvaloniaLocator.Current.GetService<IAppLogRepository>();
            appLog.AppId = Guid.NewGuid().ToString();
            appLog.SetStatus(AppStatus.AppStarted, "App started");
        }

        private static void Register()
        {
            AvaloniaLocator.CurrentMutable.Bind<IYoutubeService>().ToSingleton<YoutubeService>();

            AvaloniaLocator.CurrentMutable.Bind<ITaskDispatcher>().ToSingleton<TaskDispatcher>();

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

            AvaloniaLocator.CurrentMutable.Bind<IConfigurationRoot>()
                .ToConstant(new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json")
                                .AddJsonFile("backup.json", true, false).Build());

            AvaloniaLocator.CurrentMutable.Bind<ISyncService>()
                .ToConstant(new SyncService(AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                            AvaloniaLocator.Current.GetService<IChannelRepository>()));

            AvaloniaLocator.CurrentMutable.Bind<IBackupService>()
                .ToConstant(new BackupService(AvaloniaLocator.Current.GetService<IConfigurationRoot>(),
                                              AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                              AvaloniaLocator.Current.GetService<IItemRepository>(),
                                              AvaloniaLocator.Current.GetService<IChannelRepository>()));
        }

        private static void Shutdown()
        {
            var applog = AvaloniaLocator.Current.GetService<IAppLogRepository>();
            applog.SetStatus(AppStatus.AppClosed, "App closed");
            var context = AvaloniaLocator.Current.GetService<IContextFactory>().CreateVideoContext();
            var closedCount = applog.GetStatusCount(AppStatus.AppClosed);
            if (closedCount % 10 == 0 && closedCount != 0)
            {
                context.Database.ExecuteSqlRaw("VACUUM");
            }

            context.Dispose();
            AvaloniaLocator.Current.GetService<IPopupController>().Trigger.Dispose();
        }

        #endregion
    }
}
