using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace PhoenixFlare;

public static class AppRegistration
{
  [ComImport]
  [Guid("00021401-0000-0000-C000-000000000046")]
  internal class ShellLink
  {
  }

  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("000214F9-0000-0000-C000-000000000046")]
  internal interface IShellLink
  {
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
    void Resolve(IntPtr hwnd, int fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct WIN_32_FIND_DATAW
  {
    internal uint dwFileAttributes;
    internal System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
    internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
    internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
    internal uint nFileSizeHigh;
    internal uint nFileSizeLow;
    internal uint dwReserved0;
    internal uint dwReserved1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    internal string cFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
    internal string cAlternateFileName;
  }

  private static void RegisterApp()
  {
    const string appName = "PhoenixFlare";
    const string appPublisher = "Phoenix Systems";
    const string appVersion = "1.0.0";
    const string appGuid = "B5D00FD3-8B41-4148-A4C9-CB2E593FA1E1";
    string appExecutablePath = Process.GetCurrentProcess().MainModule.FileName;
    //string uninstallPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Uninstaller"));
    //string uninstallString = $"\"{uninstallPath}\""; // Make a simple console app to remove all files
    string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    string appShortcutFolderPath = Path.Combine(startMenuProgramsPath, appName);
    string shortcutFilePath = Path.Combine(appShortcutFolderPath, $"{appName}.lnk");
    string appShortcutTargetPath = appExecutablePath;
    RegistryKey uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
    string uninstallSubKeyPath = appGuid;
    
    // Registration logic

    try
    {
      if (!Directory.Exists(appShortcutFolderPath)) Directory.CreateDirectory(appShortcutFolderPath);
      
      IShellLink shellLink = (IShellLink)new ShellLink();
      shellLink.SetPath(appExecutablePath);
      shellLink.SetDescription(appName);
      shellLink.SetWorkingDirectory(Path.GetDirectoryName(appExecutablePath));
      shellLink.SetIconLocation(appExecutablePath, 0);
      System.Runtime.InteropServices.ComTypes.IPersistFile persistFile = (System.Runtime.InteropServices.ComTypes.IPersistFile)shellLink;
      persistFile.Save(shortcutFilePath, false);
    }
    catch (Exception e)
    {
      Console.WriteLine(e.Message);
    }

    if (uninstallKey != null)
    {
      try
      {
        RegistryKey appKey = uninstallKey.CreateSubKey(uninstallSubKeyPath);
        if (appKey != null)
        {
          appKey.SetValue("DisplayName", appName);
          appKey.SetValue("DisplayVersion", appVersion);
          appKey.SetValue("Publisher", appPublisher);
          appKey.SetValue("DisplayIcon", appExecutablePath);
          appKey.SetValue("InstallLocation", Path.GetDirectoryName(appExecutablePath));

          appKey.Close();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
      finally
      {
        uninstallKey.Close();
      }
    }
    else
    {
      Console.WriteLine("Uninstall key is null");
    }
  }

  private static bool IsAppRegisteredForCurrentUser()
  {
    string appName = "PhoenixFlare";
    string appGuid = "B5D00FD3-8B41-4148-A4C9-CB2E593FA1E1";
    string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    string appShortcutFolderPath = Path.Combine(startMenuProgramsPath, appName);
    string shortcutFilePath = Path.Combine(appShortcutFolderPath, $"{appName}.lnk");
    string uninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    string uninstallSubKeyPath = appGuid;
    bool shortcutExists = File.Exists(shortcutFilePath);
    bool registryKeyExists = false;
    using (RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(uninstallRegistryPath, false))
    {
      if (uninstallKey != null)
      {
        using (RegistryKey appKey = uninstallKey.OpenSubKey(uninstallSubKeyPath, false))
        {
          if (appKey != null)
          {
            registryKeyExists = true;
          }
        }
      }
    }

    return shortcutExists && registryKeyExists;
  }

  public static void CheckAndRegisterAppCurrentUser()
  {
    if (!IsAppRegisteredForCurrentUser())
    {
      RegisterApp();
    }
  }
}