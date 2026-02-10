namespace PhoenixFlare;

public partial class Popup : CommunityToolkit.Maui.Views.Popup
{
	public Popup(TuyaSettings currentSettings)
	{
		InitializeComponent();
		ClientIdEntry.Text = currentSettings.ClientId;
		ClientSecretEntry.Text = currentSettings.ClientSecret;
		UserIdEntry.Text = currentSettings.UserId;
		AutoStartToggle.IsToggled = currentSettings.AutoStart;
	}
	
	public TaskCompletionSource<TuyaSettings?> ResultTask { get; } = new();

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		TuyaSettings result = new()
		{
			ClientId = ClientIdEntry.Text,
			ClientSecret = ClientSecretEntry.Text,
			UserId = UserIdEntry.Text,
			RegionUrl = GetBaseUrl(RegionPicker.SelectedItem?.ToString() ?? ""),
			AutoStart = AutoStartToggle.IsToggled
		};
		ResultTask.TrySetResult(result);
		await CloseAsync();
	}
	
	private async void OnCancelClicked(object sender, EventArgs e)
	{
		ResultTask.TrySetResult(null);
		await CloseAsync();
	}
	
	private static string GetBaseUrl(string region)
	{
		return region switch
		{
			"America (us)" => "https://openapi.tuyaus.com",
			"China (cn)" => "https://openapi.tuyacn.com",
			"India (in)" => "https://openapi.tuyain.com",
			_ => "https://openapi.tuyaeu.com", // Default Europe
		};
	}
}