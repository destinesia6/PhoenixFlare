using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhoenixFlare;

public static class TuyaAuthHelper
{
	private const string _clientId = "7w8chrssfcyeu3yrhjtx";
	private const string _secret = "55e7f820eb244a2c8b4b0765792b3dca";

	private static string CalculateSignature(string clientId, string secret, string t, string method, string url, string bodyJson = "", string accessToken = "")
	{
		// 1. Generate Content-SHA256 (Hash of the body)
		string contentSha256 = HashId(bodyJson);

		// 2. Create the StringToSign 
		// Format: Method + "\n" + ContentHash + "\n" + Headers(empty) + "\n" + URL
		string stringToSign = $"{method.ToUpper()}\n{contentSha256}\n\n{url}";

		// 3. Create the final source string
		// Note: Nonce is omitted here as it's optional
		string signSource = clientId + accessToken + t + stringToSign;

		// 4. Encrypt using HMAC-SHA256
		return Hmacsha256Encrypt(signSource, secret);
	}

	private static string HashId(string str)
  {
      if (String.IsNullOrEmpty(str))
      {
          // Default hash for an empty body
          return "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
      }
      byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(str));
      return Convert.ToHexStringLower(hashBytes);
  }

    // Helper for HMAC-SHA256 (used for the final signature)
    public static string Hmacsha256Encrypt(string message, string secret)
	{
		byte[] keyByte = Encoding.UTF8.GetBytes(secret);
		byte[] messageBytes = Encoding.UTF8.GetBytes(message);

		using HMACSHA256 hmacsha256 = new(keyByte);
		byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
		return Convert.ToHexString(hashmessage).ToUpper();
	}

	public static HttpRequestMessage GeneratePOSTRequest(string body, string path, string accessToken, string baseUrl = "https://openapi.tuyaeu.com")
	{
		string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
		string sign = CalculateSignature(_clientId, _secret, timestamp, "POST", path, body, accessToken);
		
		HttpRequestMessage request = new(HttpMethod.Post, $"{baseUrl}{path}");
		request.Headers.Add("client_id", _clientId);
		request.Headers.Add("sign", sign);
		request.Headers.Add("t", timestamp);
		request.Headers.Add("access_token", accessToken);
		request.Headers.Add("sign_method", "HMAC-SHA256");
		request.Content = new StringContent(body, Encoding.UTF8, "application/json");
		return request;
	}
	
	public static HttpRequestMessage GenerateGETRequest(string path, string accessToken, string baseUrl = "https://openapi.tuyaeu.com")
	{
		string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
		string sign = CalculateSignature(_clientId, _secret, timestamp, "GET", path, accessToken: accessToken);
		
		HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl}{path}");
		request.Headers.Add("client_id", _clientId);
		request.Headers.Add("sign", sign);
		request.Headers.Add("t", timestamp);
		request.Headers.Add("access_token", accessToken);
		request.Headers.Add("sign_method", "HMAC-SHA256");
		return request;
	}
}

public class TokenResult
{
	[JsonPropertyName("access_token")] public string AccessToken { get; set; }

	[JsonPropertyName("expire_time")]
	public int ExpireTime
	{
		get => field;
		set
		{
			field = value;
			SetExpirey(value);
		}
	}
	[JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
	[JsonPropertyName("uid")] public string UID { get; set; }
	[JsonIgnore] public DateTime ExpireDateTime { get; private set; }
	private void SetExpirey(int expireTimeSeconds)
	{
		ExpireDateTime = DateTime.Now.AddSeconds(expireTimeSeconds);
	}
}

public class AuthResponse<T>
{
	[JsonPropertyName("result")] public T Result { get; set; }
}

public class Response<T>
{
	[JsonPropertyName("code")] public int Code { get; set; }
	[JsonPropertyName("success")] public bool Success { get; set; }
	[JsonPropertyName("msg")] public string Message { get; set; }
	[JsonPropertyName("result")] public T Result { get; set; }
}

public class DeviceResult : INotifyPropertyChanged
{
	[JsonPropertyName("id")] public string Id { get; set; }
	[JsonPropertyName("uid")] public string Uid { get; set; }
	[JsonPropertyName("local_key")] public string LocalKey { get; set; }
	[JsonPropertyName("category")] public string Category { get; set; }
	[JsonPropertyName("product_id")] public string ProductId { get; set; }
	[JsonPropertyName("sub")] public bool Sub { get; set; }
	[JsonPropertyName("uuid")] public string Uuid { get; set; }
	[JsonPropertyName("owner_id")] public string OwnerId { get; set; }
	[JsonPropertyName("online")] public bool Online { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; }
	[JsonPropertyName("ip")] public string Ip { get; set; }
	[JsonPropertyName("time_zone")] public string TimeZone { get; set; }
	[JsonPropertyName("create_time")] public long CreateTime { get; set; }
	[JsonPropertyName("update_time")] public long UpdateTime { get; set; }
	[JsonPropertyName("active_time")] public long ActiveTime { get; set; }
	
	public uint BoundVKey { get; set; }
	
	[JsonPropertyName("status")]
	public List<Status> Status { get; set; }

	[JsonIgnore]
	public string BoundKeyName
	{
		get;
		set
		{
			field = value;
			OnPropertyChanged();
		}
	} = "None";

	[JsonIgnore]
	public bool IsLightOn
	{
		get => Status?.FirstOrDefault(s => s.Code == "switch_led")?.Value?.ToString()?.ToLower() == "true";
		set 
		{
			var s = Status?.FirstOrDefault(x => x.Code == "switch_led");
			if (s != null)
			{
				s.Value = value;
				OnPropertyChanged(); // This triggers the Switch move
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
	public void OnPropertyChanged([CallerMemberName] string name = "") =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Status
{
	[JsonPropertyName("code")] public string Code { get; set; }
	[JsonPropertyName("value")] public object Value { get; set; }
}

public class KeyBindEntry
{
	public string DeviceId { get; set; }
	public string KeyName { get; set; }
	public uint VKey { get; set; }
}

public class TuyaSettings
{
	public string ClientId { get; set; } = "";
	public string ClientSecret { get; set; } = "";
	public string UserId { get; set; } = "";
	public string RegionUrl { get; set; } = "https://openapi.tuyaeu.com"; // Default to EU
	public bool AutoStart { get; set; }
}