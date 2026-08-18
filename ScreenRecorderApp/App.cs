using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ScreenRecorderApp.Services;
using ScreenRecorderApp.Views;

namespace ScreenRecorderApp;

public partial class App : Application
{

	private async void App_Startup(object sender, StartupEventArgs e)
	{
		base.DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
		if (SupabaseService.TryLoadSession())
		{
			new MainWindow().Show();
		}
		else
		{
			new LoginWindow().Show();
		}
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		LogException(e.Exception);
		MessageBox.Show("WystÄ…piĹ‚ nieoczekiwany bĹ‚Ä…d:\n" + e.Exception.Message, "ScreenRecorderApp", MessageBoxButton.OK, MessageBoxImage.Hand);
		e.Handled = true;
	}

	private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			LogException(ex);
		}
	}

	private static void LogException(Exception ex)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "logs");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, $"error_{DateTime.Now:yyyy-MM-dd}.log"), $"[{DateTime.Now:HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
		}
		catch
		{
		}
	}
}
