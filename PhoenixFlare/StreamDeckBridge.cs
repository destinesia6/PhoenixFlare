using System.Text;
using System.Text.Json;

using CommunityToolkit.Mvvm.Messaging;
using Fleck;
using WatsonWebsocket; // Optional, or use Shell/MessagingCentery

public class StreamDeckBridge
{
	private WebSocketServer _server;

	public void Start()
	{
		// Listen on localhost port 9020
		_server = new WebSocketServer("ws://127.0.0.1:9020");
		_server.Start(socket =>
		{
			socket.OnOpen = () =>
			{
				Console.WriteLine("Connected to Stream Deck");
			};
			socket.OnMessage = message => OnMessageReceived(message, socket);
		});
	}

	private void OnMessageReceived(string rawMessage, IWebSocketConnection socket)
	{
        
		MainThread.BeginInvokeOnMainThread(() => 
		{
			if (rawMessage == "FETCH_DEVICES")
			{
				// Requesting data from MainPage
				WeakReferenceMessenger.Default.Send(new RequestDeviceListMessage(devices => {
					string json = JsonSerializer.Serialize(devices);
					socket.Send("DEVICE_LIST:" + json);
				}));
			}
			else if (rawMessage.StartsWith("TOGGLE:"))
			{
				string deviceId = rawMessage.Split(':')[1];
				WeakReferenceMessenger.Default.Send(new ToggleDeviceMessage(deviceId, status =>
				{
					socket.Send($"STATUS:{deviceId}:{status}");
				}));
			}
		});
	}
}

public record RequestDeviceListMessage(Action<IEnumerable<object>> Callback);

public record ToggleDeviceMessage(string DeviceId, Action<bool> Callback);