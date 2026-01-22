using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Input;

namespace PhoenixFlare;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		AddTrayIcon();
		PageContainer.Children.Remove(_trayPopup);
		ShowTrayIcon();
		InitializeApp();
	}
	
	private TaskbarIcon _trayPopup = new();
	private TokenResult? _accessToken;
	private HttpClient _httpClient = new();
	public ObservableCollection<DeviceResult> Devices { get; } = [];
	
#if WINDOWS
	private IntPtr _oldWndProc = IntPtr.Zero;
	private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	private WinProc? _newWndProc;
	private IntPtr _windowHandle;

	public void InitializeApp()
	{
#if WINDOWS
		UnsubscribeGlobalHotkeys();
#endif
		DevicesListView.ItemsSource = Devices;
		LoadSettingsFromBson();
#if WINDOWS
		SetupGlobalHotkeys(); // Initialize the Windows message listener
#endif
		Task.Run(async () =>
		{
			await SetAccessToken();
			await GetDeviceList();
			MainThread.BeginInvokeOnMainThread(LoadBsonAndRegisterKeys);
		});
	}

	private void SetupGlobalHotkeys()
	{
		MauiWinUIWindow? window = Application.Current?.Windows[0].Handler?.PlatformView as MauiWinUIWindow;
		_windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);

		// Create a delegate for our new window procedure
		_newWndProc = SubClassWndProc;
    
		// Hook the window
		_oldWndProc = WindowHelper.SetWindowLongPtr(_windowHandle, WindowHelper.GWL_WNDPROC, 
			Marshal.GetFunctionPointerForDelegate(_newWndProc));
	}
	
	private void UnsubscribeGlobalHotkeys()
	{
		// Check if we actually have a handle and a saved old procedure
		if (_windowHandle == IntPtr.Zero || _oldWndProc == IntPtr.Zero) return;
		// Restore the original window procedure
		WindowHelper.SetWindowLongPtr(_windowHandle, WindowHelper.GWL_WNDPROC, _oldWndProc);
        
		// Reset our variables to be safe
		_oldWndProc = IntPtr.Zero;
		_newWndProc = null;
	}
	
	private IntPtr SubClassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if (msg != WindowHelper.WM_HOTKEY) return WindowHelper.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
		int id = (int)wParam;
		// Run on a background thread so we don't block the UI loop
		Task.Run(async () => await HandleGlobalHotkey(id));

		// Always call the original window procedure
		return WindowHelper.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
	}

	private async Task HandleGlobalHotkey(int id)
	{
		// 1. Find the actual object instance in the list
		DeviceResult? device = Devices.FirstOrDefault(d => d.Id.GetHashCode() == id);
		if (device == null) return;

		bool targetState = !await IsDeviceOn(device.Id);
		bool success = await ToggleLight(device.Id, targetState);

		if (success)
		{
			// 2. Update the property on the UI thread
			MainThread.BeginInvokeOnMainThread(() =>
			{
				device.IsLightOn = targetState; 
				// Setting this triggers OnPropertyChanged inside the class
			});
#if WINDOWS
			UpdateTrayMenu();
#endif
		}
	}
#endif
	
	private async Task ShowSettingsPopup()
	{
#if WINDOWS
		_settings.AutoStart = AutoStartService.GetAutoStart();
#endif
		Popup popup = new(_settings);
		await this.ShowPopupAsync(popup);
		TuyaSettings? result = await popup.ResultTask.Task;

		if (result is not null)
		{
#if WINDOWS
			if (result.AutoStart != _settings.AutoStart)
			{
				AutoStartService.SetAutoStart(result.AutoStart);
			}
#endif
			_settings = result;
			SaveSettingsToBson();
        
			// Re-connect with new credentials and get the devices for that account
			await SetAccessToken();
			await GetDeviceList();
			LoadBsonAndRegisterKeys();
		}
	}

	private async void OnOpenSettingsClicked(object sender, EventArgs e) => await ShowSettingsPopup();

	private void OnBindKeyClicked(object sender, EventArgs e)
	{
		DeviceResult? device = (DeviceResult?)((Button)sender).CommandParameter;
		MainThread.BeginInvokeOnMainThread(() => device?.BoundKeyName = "Listening...");

#if WINDOWS
		MauiWinUIWindow? window = Application.Current?.Windows[0].Handler?.PlatformView as MauiWinUIWindow;
    
		// We must use the specific KeyEventHandler type
		KeyEventHandler? handler = null;
    
		handler = (_, ex) =>
		{
			uint vKey = (uint)ex.Key;
			int hotKeyId = device?.Id.GetHashCode() ?? -1;

			WindowHelper.UnregisterHotKey(_windowHandle, hotKeyId);
			MainThread.BeginInvokeOnMainThread(() => device?.BoundKeyName = ex.Key.ToString());
			device?.BoundVKey = vKey;

			// Register with MOD_NONE for single-key global trigger
			bool success = WindowHelper.RegisterHotKey(_windowHandle, hotKeyId, 
				WindowHelper.MOD_NONE, vKey);
        
			if (success) 
			{
				SaveBindsToBson();
			}
        
			// Remove the listener using the same delegate type
			window?.Content.KeyDown -= handler;
			ex.Handled = true; // Prevents the key from being processed further by the UI
		};

		window?.Content.KeyDown += handler;
#endif
	}

	private void SaveBindsToBson()
	{
		string path = Path.Combine(FileSystem.AppDataDirectory, "keys.bson");
    
		// Map your current devices to the storage model
		List<KeyBindEntry> data = Devices.Select(d => new KeyBindEntry 
		{ 
			DeviceId = d.Id, 
			KeyName = d.BoundKeyName, 
			VKey = d.BoundVKey 
		}).ToList();

		using MemoryStream ms = new();
		using Newtonsoft.Json.Bson.BsonDataWriter writer = new(ms);
    
		// Use the Newtonsoft Serializer here
		Newtonsoft.Json.JsonSerializer serializer = new();
		serializer.Serialize(writer, data);
    
		File.WriteAllBytes(path, ms.ToArray());
	}
	
	private void LoadBsonAndRegisterKeys()
	{
		string path = Path.Combine(FileSystem.AppDataDirectory, "keys.bson");
		if (!File.Exists(path)) return;

		try 
		{
			byte[] bsonData = File.ReadAllBytes(path);
			using MemoryStream ms = new(bsonData);
			using Newtonsoft.Json.Bson.BsonDataReader reader = new(ms);
			reader.ReadRootValueAsArray = true;

			Newtonsoft.Json.JsonSerializer serializer = new();
			List<KeyBindEntry>? savedBinds = serializer.Deserialize<List<KeyBindEntry>>(reader);

			if (savedBinds == null) return;
			foreach (KeyBindEntry bind in savedBinds)
			{
				DeviceResult? device = Devices.FirstOrDefault(d => d.Id == bind.DeviceId);
				if (device == null) continue;
				device.BoundKeyName = bind.KeyName;
				device.BoundVKey = bind.VKey;
#if WINDOWS
				WindowHelper.RegisterHotKey(_windowHandle, device.Id.GetHashCode(), 
					WindowHelper.MOD_NONE, device.BoundVKey);
#endif
			}
		}
		catch (Exception ex) 
		{
			Console.WriteLine($"Error loading keybinds: {ex.Message}");
		}
	}
	
	private void SaveSettingsToBson()
	{
		string path = Path.Combine(FileSystem.AppDataDirectory, "settings.bson");
		using MemoryStream ms = new();
		using Newtonsoft.Json.Bson.BsonDataWriter writer = new(ms);
		new Newtonsoft.Json.JsonSerializer().Serialize(writer, _settings);
		File.WriteAllBytes(path, ms.ToArray());
	}

	private void LoadSettingsFromBson()
	{
		string path = Path.Combine(FileSystem.AppDataDirectory, "settings.bson");
		if (!File.Exists(path))
		{
			Task.Run(async () => await ShowSettingsPopup());
			return;
		}

		try
		{
			byte[] bsonData = File.ReadAllBytes(path);
			using MemoryStream ms = new(bsonData);
			using Newtonsoft.Json.Bson.BsonDataReader reader = new(ms);
			Newtonsoft.Json.JsonSerializer serializer = new();
			TuyaSettings? savedBinds = serializer.Deserialize<TuyaSettings>(reader);
			if (savedBinds is null)
			{
				Task.Run(async () => await ShowSettingsPopup());
				return; 
			}
			_settings = savedBinds;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
		}
	}
	
	private TuyaSettings _settings = new();

	public async Task SetAccessToken()
	{
		string t = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
		const string method = "GET";
    
		// 1. The SHA256 of an empty body (mandatory for GET)
		const string emptyBodyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    
		// 2. The URL Path + Query
		const string url = "/v1.0/token?grant_type=1";

		// 3. Build StringToSign: Method + \n + Hash + \n + Headers(empty) + \n + URL
		const string stringToSign = $"{method}\n{emptyBodyHash}\n\n{url}";

		// 4. Build the final sign source: clientId + t + stringToSign
		string signSource = _settings.ClientId + t + stringToSign;

		// 5. HMAC-SHA256
		string sign = TuyaAuthHelper.Hmacsha256Encrypt(signSource, _settings.ClientSecret);

		// 6. Execute Request
		HttpRequestMessage request = new(HttpMethod.Get, $"{_settings.RegionUrl}{url}");
		request.Headers.Add("client_id", _settings.ClientId);
		request.Headers.Add("t", t);
		request.Headers.Add("sign", sign);
		request.Headers.Add("sign_method", "HMAC-SHA256");

		HttpResponseMessage response = await _httpClient.SendAsync(request);
		AuthResponse<TokenResult>? deserializedResponse = JsonSerializer.Deserialize<AuthResponse<TokenResult>>(await response.Content.ReadAsStringAsync());
		if (deserializedResponse is not null) _accessToken = deserializedResponse.Result;
	}

	public async Task RefreshToken()
	{
		string t = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
		const string method = "GET";
    
		// 1. The SHA256 of an empty body (mandatory for GET)
		const string emptyBodyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    
		// 2. The URL Path + Query
		string url = $"/v1.0/token/{_accessToken?.RefreshToken}";

		// 3. Build StringToSign: Method + \n + Hash + \n + Headers(empty) + \n + URL
		string stringToSign = $"{method}\n{emptyBodyHash}\n\n{url}";

		// 4. Build the final sign source: clientId + t + stringToSign
		string signSource = _settings.ClientId + t + stringToSign;

		// 5. HMAC-SHA256
		string sign = TuyaAuthHelper.Hmacsha256Encrypt(signSource, _settings.ClientSecret);

		// 6. Execute Request
		HttpRequestMessage request = new(HttpMethod.Get, $"{_settings.RegionUrl}{url}");
		request.Headers.Add("client_id", _settings.ClientId);
		request.Headers.Add("t", t);
		request.Headers.Add("sign", sign);
		request.Headers.Add("sign_method", "HMAC-SHA256");
		
		HttpResponseMessage response = await _httpClient.SendAsync(request);
		AuthResponse<TokenResult>? deserializedResponse = JsonSerializer.Deserialize<AuthResponse<TokenResult>>(await response.Content.ReadAsStringAsync());
		if (deserializedResponse is not null && !String.IsNullOrWhiteSpace(deserializedResponse.Result.AccessToken))
		{
			_accessToken = deserializedResponse.Result;
		}
		else
		{
			await SetAccessToken();
		}
	}

	public async Task GetDeviceList()
	{
		try
		{
			if (_accessToken?.ExpireDateTime <= DateTime.Now) await RefreshToken();
			string url = $"/v1.0/users/{_settings.UserId}/devices";
			HttpRequestMessage request = TuyaAuthHelper.GenerateGETRequest(url, _accessToken?.AccessToken ?? "", _settings.RegionUrl);
			HttpResponseMessage response = await _httpClient.SendAsync(request);
			string jsonResponse = await response.Content.ReadAsStringAsync();
			Response<List<DeviceResult>>? desResponse =
				JsonSerializer.Deserialize<Response<List<DeviceResult>>>(jsonResponse);
			if (desResponse?.Success == true)
			{
				MainThread.BeginInvokeOnMainThread(() => {
					Devices.Clear();
					foreach(DeviceResult device in desResponse.Result)
						Devices.Add(device);
				});
#if WINDOWS
				UpdateTrayMenu();
#endif
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
		}
	}

	// Simplified logic for a Tuya Request
	public async Task<bool> ToggleLight(string deviceId, bool turnOn)
	{
		if (_accessToken?.ExpireDateTime <= DateTime.Now) await RefreshToken();
		string body = "{\"commands\":[{\"code\":\"switch_led\",\"value\":" + turnOn.ToString().ToLower();
		body += "}]}";
		string url = $"/v1.0/devices/{deviceId}/commands";
		HttpRequestMessage request = TuyaAuthHelper.GeneratePOSTRequest(body, url, _accessToken?.AccessToken ?? "", _settings.RegionUrl);
		HttpResponseMessage response = await _httpClient.SendAsync(request);
		return response.IsSuccessStatusCode;
	}
	
	public async Task<bool> IsDeviceOn(string deviceId)
	{
		if (String.IsNullOrWhiteSpace(deviceId)) return false;
		if (_accessToken?.ExpireDateTime <= DateTime.Now) await RefreshToken();
		string url = $"/v1.0/devices/{deviceId}/status";
		HttpRequestMessage request = TuyaAuthHelper.GenerateGETRequest(url, _accessToken?.AccessToken ?? "", _settings.RegionUrl);
		HttpResponseMessage response = await _httpClient.SendAsync(request);
		string responseJson = await response.Content.ReadAsStringAsync();
		Response<List<Status>>? desResponse = JsonSerializer.Deserialize<Response<List<Status>>>(responseJson);
		if (desResponse?.Result.FirstOrDefault(s => s.Code == "switch_led")?.Value is JsonElement valueBool) return valueBool.GetBoolean();
		return false;
	}
	
	private async void OnDeviceToggled(object sender, ToggledEventArgs e)
	{
		if (sender is not Switch sw) return;
		// We use AutomationId to store the Device ID string
		string deviceId = sw.AutomationId;
		bool deviceStatus = await IsDeviceOn(deviceId);
		bool switchStatus = e.Value;
		
		await ToggleLight(deviceId, !deviceStatus);
		
		if (switchStatus != !deviceStatus)
		{
			DeviceResult? selectedDevice = Devices.FirstOrDefault(d => d.Id == deviceId);
			MainThread.BeginInvokeOnMainThread(() => selectedDevice?.IsLightOn = !deviceStatus);
		}
#if WINDOWS
		UpdateTrayMenu();
#endif
	}
	
	public static string Encrypt(string str, string? secret)
	{
		secret ??= "";
		UTF8Encoding encoding = new();
		byte[] keyByte = encoding.GetBytes(secret);
		byte[] messageBytes = encoding.GetBytes(str);
		using HMACSHA256 hmacsha256 = new(keyByte);
		byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
		StringBuilder builder = new();
		foreach (byte t in hashmessage)
		{
			builder.Append(t.ToString("x2"));
		}
		return builder.ToString().ToUpper();
	}
	
	//------ Tray icon code -------
#if WINDOWS	
	private void ShowTrayIcon()  
	{  
		if (!PageContainer.Contains(_trayPopup))  
		{  
			PageContainer.Children.Add(_trayPopup);  
		}  
	}
	
	private async void AddTrayIcon() // Creates the object for the tray icon, which can be added when the app is closed  
	{
		Assembly assmebly = Assembly.GetExecutingAssembly();
		string resourceName = $"{assmebly.GetName().Name}.Resources.phoenixtrayicon.ico";
		await using Stream? stream = assmebly.GetManifestResourceStream(resourceName);
		if (stream != null)
		{
			_trayPopup = new TaskbarIcon
			{
				Id = new Guid("96bc756c-03d3-4927-991f-06d203929e7c"),
				Icon = new System.Drawing.Icon(stream),
				LeftClickCommand = ShowWindowCommand,
				NoLeftClickDelay = true
			};

			MenuFlyout menu = [];
			MenuFlyoutItem exitMenuItem = new()
			{
				Command = CloseAppCommand,
				Text = "Exit"
			};
			menu.Add(exitMenuItem);
			FlyoutBase.SetContextFlyout(_trayPopup, menu);
		}
		else
		{
			Console.WriteLine("Tray icon cannot be created, icon was not found.");
		}
	}
	
	private void UpdateTrayMenu()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			// Get the current menu
			if (FlyoutBase.GetContextFlyout(_trayPopup) is not MenuFlyout menu) return;

			// 1. Clear existing items (or keep the Exit item if you prefer)
			menu.Clear();

			// 2. Add Device Items
			foreach (DeviceResult device in Devices)
			{
				MenuFlyoutItem deviceItem = new()
				{
					Text = $"{(device.IsLightOn ? "●" : "○")} {device.Name}",
					Command = new Command(async void () => await ToggleLightFromTray(device))
				};

				menu.Add(deviceItem);
			}

			// 3. Re-add the Exit separator and item
			menu.Add(new MenuFlyoutSeparator());
			menu.Add(new MenuFlyoutItem
			{
				Text = "Exit",
				Command = CloseAppCommand
			});
		});
	}

	private async Task ToggleLightFromTray(DeviceResult device)
	{
		bool lightOn = !await IsDeviceOn(device.Id);
		bool success = await ToggleLight(device.Id, lightOn);
		if (success)
		{
			MainThread.BeginInvokeOnMainThread(() => device.IsLightOn = lightOn);
		}
	}
	
	[RelayCommand]
  public void ShowWindow()
  {
      #if WINDOWS
      AppClosingHandler.ShowApp();
      _trayPopup.IsEnabled = false;
      #endif
  }

  [RelayCommand]
  public void CloseApp()
  {
      Application.Current?.Quit();
  }
#endif	
	//-----------------------------

	public string GetSignatureString(string method, string content, string url)
	{
		string stringToSign = method + "\n" + sha256_hash(content) + "\n" + url;
		string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
		return _settings.ClientId + timestamp + stringToSign;
	}
	
	public static string sha256_hash(string value)
  {
      StringBuilder sb = new();
      Encoding enc = Encoding.UTF8;
      byte[] result = SHA256.HashData(enc.GetBytes(value));

      foreach (byte b in result)
          sb.Append(b.ToString("x2"));

      return sb.ToString();
  }
}