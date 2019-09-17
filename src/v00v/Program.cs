﻿using System;
using Avalonia;
using Avalonia.Logging.Serilog;
using v00v.MainApp;

namespace v00v
{
    class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .LogToDebug();

    }
}
