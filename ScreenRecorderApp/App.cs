using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ScreenRecorderApp.Services;
using ScreenRecorderApp.Views;

namespace ScreenRecorderApp;

public partial class App : Application
{

	public App()
	{
		base.Startup += App_Startup;
	}

	private async void App_Startup(object sender, StartupEventArgs e)
	{
		string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
		File.WriteAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] App starting...\n");
		try
		{
			base.DispatcherUnhandledException += OnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
			File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Event handlers registered.\n");

			// Check if we just updated successfully
			if (File.Exists(UpdaterService.LastVersionFile))
			{
				try
				{
					string targetVersionStr = File.ReadAllText(UpdaterService.LastVersionFile).Trim();
					Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

					if (currentVersion.ToString().StartsWith(targetVersionStr) || targetVersionStr.StartsWith(currentVersion.ToString()))
					{
						File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Detected successful update to version {currentVersion}.\n");
						
						// Show Windows Notification
						NotificationService.ShowToast(
							"Aktualizacja zakończona pomyślnie!",
							$"Aplikacja Recorder została zaktualizowana do wersji {currentVersion}."
						);

						// Show Changelog if cached
						if (File.Exists(UpdaterService.ChangelogCachePath))
						{
							string changelogText = File.ReadAllText(UpdaterService.ChangelogCachePath);
							File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Displaying ChangelogWindow...\n");
							ChangelogWindow changelogWin = new ChangelogWindow(changelogText);
							changelogWin.ShowDialog();
						}
					}
				}
				catch (Exception exVal)
				{
					File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Error during post-update logic: {exVal.Message}\n");
				}
				finally
				{
					try { File.Delete(UpdaterService.LastVersionFile); } catch {}
					try { File.Delete(UpdaterService.ChangelogCachePath); } catch {}
				}
			}

			File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Loading session...\n");
			bool hasSession = SupabaseService.TryLoadSession();
			File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Session loaded. HasSession: {hasSession}\n");

			if (hasSession)
			{
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Creating MainWindow...\n");
				MainWindow mainWin = new MainWindow();
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Showing MainWindow...\n");
				mainWin.Show();
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] MainWindow shown.\n");
			}
			else
			{
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Creating LoginWindow...\n");
				LoginWindow loginWin = new LoginWindow();
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Showing LoginWindow...\n");
				loginWin.Show();
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] LoginWindow shown.\n");
			}

			// Run silent update check in background after a short delay
			_ = Task.Run(async () =>
			{
				try
				{
					await Task.Delay(5000);
					await UpdaterService.CheckAndPerformUpdateAsync(silent: true);
				}
				catch (Exception exUp)
				{
					File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] Background update check failed: {exUp.Message}\n");
				}
			});
		}
		catch (Exception ex)
		{
			File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] CRASH during startup: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}\n");
			if (ex.InnerException != null)
			{
				File.AppendAllText(logFile, $"Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n");
			}
			throw;
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
