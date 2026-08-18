using System;
using System.Threading.Tasks;
using System.Windows;
using ScreenRecorderApp.Services;

namespace ScreenRecorderApp.Views;

public partial class PreloaderWindow : Window
{
	private UpdaterService.UpdateInfo? _updateInfo;

	public PreloaderWindow()
	{
		InitializeComponent();
		Loaded += PreloaderWindow_Loaded;
	}

	private async void PreloaderWindow_Loaded(object sender, RoutedEventArgs e)
	{
		// Give the window a moment to render the spinner
		await Task.Delay(1000);
		await CheckForUpdatesAsync();
	}

	private async Task CheckForUpdatesAsync()
	{
		try
		{
			_updateInfo = await UpdaterService.CheckForUpdateInfoAsync();
			if (_updateInfo != null && _updateInfo.IsUpdateAvailable)
			{
				// Update available: transition UI to prompt state
				LoadingContainer.Visibility = Visibility.Collapsed;
				UpdatePromptContainer.Visibility = Visibility.Visible;
				ActionButtonsContainer.Visibility = Visibility.Visible;
				UpdateVersionText.Text = $"Wersja {_updateInfo.VersionTag}";
			}
			else
			{
				// No updates: go to main app flow
				ContinueToApp();
			}
		}
		catch (Exception ex)
		{
			// In case of any error, fail gracefully and load the app
			System.Diagnostics.Debug.WriteLine("Preloader error: " + ex.Message);
			ContinueToApp();
		}
	}

	private async void UpdateButton_Click(object sender, RoutedEventArgs e)
	{
		if (_updateInfo == null) return;

		// Disable buttons and transition back to loader with download text
		ActionButtonsContainer.Visibility = Visibility.Collapsed;
		UpdatePromptContainer.Visibility = Visibility.Collapsed;
		
		StatusText.Text = "Pobieranie aktualizacji...";
		LoadingContainer.Visibility = Visibility.Visible;

		// Perform update
		bool success = await Task.Run(() => UpdaterService.DownloadAndInstallAsync(_updateInfo));
		if (!success)
		{
			MessageBox.Show("Błąd pobierania aktualizacji. Uruchamianie aplikacji...", "Aktualizacja", MessageBoxButton.OK, MessageBoxImage.Warning);
			ContinueToApp();
		}
	}

	private void SkipButton_Click(object sender, RoutedEventArgs e)
	{
		ContinueToApp();
	}

	private void ContinueToApp()
	{
		try
		{
			bool hasSession = SupabaseService.TryLoadSession();
			if (hasSession)
			{
				MainWindow mainWin = new MainWindow();
				mainWin.Show();
			}
			else
			{
				LoginWindow loginWin = new LoginWindow();
				loginWin.Show();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Błąd startu aplikacji: " + ex.Message, "ScreenRecorderApp", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		Close();
	}
}
