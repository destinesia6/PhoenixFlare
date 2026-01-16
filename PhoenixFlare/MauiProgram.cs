using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
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
              appWindow?.Hide();
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