using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace PhoenixFlare;

public partial class App : Application
{
	public App()
	{
#if WINDOWS
		AppRegistration.CheckAndRegisterAppCurrentUser();
		SystemEvents.PowerModeChanged += OnPowerModeChanged;
#endif
		InitializeComponent();
		Microsoft.UI.Xaml.Application.Current.UnhandledException += (sender, e) =>
		{
			File.AppendAllText(Path.Combine(FileSystem.AppDataDirectory, "crash.txt"), $"{DateTime.Now}: {e.Exception}\n");
		};
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Window window = new(new AppShell());
		#if WINDOWS
		window.Width = 650;
		window.Height = 750;
		
		DisplayInfo displayInfo = DeviceDisplay.Current.MainDisplayInfo;
    window.X = (displayInfo.Width / displayInfo.Density - window.Width) / 2;
    window.Y = (displayInfo.Height / displayInfo.Density - window.Height) / 2;
		#endif
		return window;
	}
	
	private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			// Wait a tiny bit for the network stack to actually initialize 
			// after wake-up before trying to refresh devices.
			Task.Delay(2000).ContinueWith(_ =>
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					switch (Current?.MainPage)
					{
						// Check if the current page is your MainPage
						case Shell { CurrentPage: MainPage mainPage }:
							mainPage.InitializeApp();
							break;
						case MainPage directPage:
							directPage.InitializeApp();
							break;
					}
				});
			});
		}
	}
}