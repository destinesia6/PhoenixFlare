using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Serilog;
using Serilog.Core;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace PhoenixFlare;

public static class MauiProgram
{
	private static bool _hasBeenHidden = false;
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		
		string logPath = Path.Combine(FileSystem.AppDataDirectory, "log.txt");
		Logger logger = new LoggerConfiguration().WriteTo.File(logPath, rollingInterval: RollingInterval.Day).CreateLogger();
		builder.Logging.AddSerilog(logger);
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			}).ConfigureLifecycleEvents(events =>
      {
#if WINDOWS
        events.AddWindows(windowLifecycleBuilder =>
        {
          windowLifecycleBuilder.OnWindowCreated(window =>
          {
            window.Activated += (sender, args) =>
            {
              if (_hasBeenHidden) return;
              AppWindow? appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(window)));
              //appWindow?.Hide();
              _hasBeenHidden = true;
            };
          });
        });
#endif
      });
			
#if WINDOWS
		AppClosingHandler.HandleAppClosing(builder);
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}