namespace PhoenixFlare;
using Microsoft.Win32;

public class AutoStartService
{
	private const string registryKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
	private const string appName = "PhoenixFlare";
	private static readonly string? _appPath = GetExecutablePath();

	private static string? GetExecutablePath()
	{
		string? entryLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
		if (String.IsNullOrEmpty(entryLocation)) return null;
		if (entryLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
		{
			string? appFolder = Path.GetDirectoryName(entryLocation);
			return appFolder != null ? Path.Combine(appFolder, Path.GetFileNameWithoutExtension(entryLocation) + ".exe") : null;
		}

		return entryLocation;
	}

	public static bool GetAutoStart()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKeyPath);
		if (key == null) return false;
		string? storedPath = key.GetValue(appName) as string;
		string normalizedStoredPath = storedPath?.Trim('"') ?? String.Empty;
		string normalizedAppPath = _appPath?.Trim('"') ?? String.Empty;
		return normalizedStoredPath.Equals(normalizedAppPath, StringComparison.OrdinalIgnoreCase);
	}

	public static void SetAutoStart(bool isEnabled)
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKeyPath, true);
		if (key == null) return;
		if (isEnabled)
		{
			if (_appPath != null)
			{
				string pathWithQuotes = $"\"{_appPath}\"";
				key.SetValue(appName, pathWithQuotes);
			}
		}
		else
		{
			key.DeleteValue(appName, false);
		}
	}
}