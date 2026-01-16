using Microsoft.Extensions.DependencyInjection;

namespace PhoenixFlare;

public partial class App : Application
{
	public App()
	{
#if WINDOWS
		AppRegistration.CheckAndRegisterAppCurrentUser();
#endif
		InitializeComponent();
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
}