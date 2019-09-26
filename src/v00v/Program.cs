using System;
using AutoMapper;
using Avalonia;
using Avalonia.Logging.Serilog;
using v00v.MainApp;
using v00v.Services.ContentProvider;
using v00v.Services.Database;
using v00v.Services.Persistence;
using v00v.Services.Persistence.Mappers;
using v00v.Services.Persistence.Repositories;
using v00v.ViewModel.Popup;

namespace v00v
{
    class Program
    {
        #region Static Methods

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToDebug();

        [STAThread]
        private static void Main(string[] args)
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

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        #endregion
    }
}
