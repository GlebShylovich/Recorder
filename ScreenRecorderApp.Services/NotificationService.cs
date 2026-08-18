using System;
using System.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ScreenRecorderApp.Services;

public static class NotificationService
{
	private const string AppId = "ScreenRecorderApp";

	public static void ShowToast(string title, string message)
	{
		try
		{
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
			Debug.WriteLine("Windows Notification error: " + ex.Message);
		}
	}
}
