using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ScreenRecorderApp.Services;

public static class NotificationService
{
	private const string AppId = "ScreenRecorderApp";

	public static void RegisterApp()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppId))
			{
				if (key != null)
				{
					key.SetValue("DisplayName", "Screen Recorder");
					string exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
					string dir = Path.GetDirectoryName(exePath) ?? string.Empty;
					string iconPath = Path.Combine(dir, "app.ico");
					if (File.Exists(iconPath))
					{
						key.SetValue("IconUri", iconPath);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Registry registration failed: " + ex.Message);
		}
	}

	public static void ShowToast(string title, string message)
	{
		try
		{
			// Ensure registered in system
			RegisterApp();

			string xml = $@"
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{System.Security.SecurityElement.Escape(title)}</text>
            <text>{System.Security.SecurityElement.Escape(message)}</text>
        </binding>
    </visual>
</toast>";

			XmlDocument doc = new XmlDocument();
			doc.LoadXml(xml);

			ToastNotification toast = new ToastNotification(doc);
			ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
		}
		catch (Exception ex)
		{
			try
			{
				string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
				System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Notification failed: {ex.Message}\n{ex.StackTrace}\n");
			}
			catch {}
		}
	}
}

