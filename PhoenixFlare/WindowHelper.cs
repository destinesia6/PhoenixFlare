namespace PhoenixFlare;

using System.Runtime.InteropServices;

public static class WindowHelper
{
	[DllImport("user32.dll")]
	public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll")]
	public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("user32.dll")]
	public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	public const int GWL_WNDPROC = -4;
	public const uint MOD_NONE = 0x0000; // No modifier
	public const int WM_HOTKEY = 0x0312;
}