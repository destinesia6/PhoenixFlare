using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

namespace PhoenixFlare;

public static class AppClosingHandler
{
	private static AppWindow? _appWindow;
	private static bool _isClosingPrevented;
	public static event EventHandler PageHide;

	public static void HandleAppClosing(MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if WINDOWS
			events.AddWindows(windowLifetimeBuilder =>
			{
				windowLifetimeBuilder.OnWindowCreated(window =>
				{
					_appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(window)));
					if (_appWindow != null)
					{
						_appWindow.Closing += OnAppWindowClosing;
					}
				});
			});
#endif
		});
	}

	public static void ShowApp()
	{
		_appWindow?.Show();
		_isClosingPrevented = false;
	}

	public static void OnAppWindowClosing(object? sender, AppWindowClosingEventArgs args)
	{
		if (_isClosingPrevented) return;
		args.Cancel = true;
		_isClosingPrevented = true;
		if (_appWindow != null)
		{
			_appWindow.Hide();
		}

		//PageHide.Invoke(null, EventArgs.Empty);
	}
}