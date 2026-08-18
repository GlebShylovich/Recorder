using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace ScreenRecorderApp.Services;

public static class UpdaterService
{
	private const string RepoUrl = "https://api.github.com/repos/GlebShylovich/Recorder/releases/latest";
	private static readonly HttpClient Http = new HttpClient();

	private static string TempInstallerPath => Path.Combine(Path.GetTempPath(), "SetupScreenRecorder_Update.exe");
	public static string ChangelogCachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "changelog_cache.txt");
	public static string LastVersionFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "last_version.txt");

	static UpdaterService()
	{
		Http.DefaultRequestHeaders.Add("User-Agent", "ScreenRecorderApp-Updater");
	}

	public static async Task<bool> CheckAndPerformUpdateAsync(bool silent = true)
	{
		try
		{
			// 1. Get current assembly version
			Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

			// 2. Fetch latest release from GitHub
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, RepoUrl);
			using HttpResponseMessage res = await Http.SendAsync(req);
			if (!res.IsSuccessStatusCode)
			{
				return false;
			}

			string json = await res.Content.ReadAsStringAsync();
			JsonNode? releaseNode = JsonNode.Parse(json);
			if (releaseNode == null)
			{
				return false;
			}

			string? tagName = releaseNode["tag_name"]?.GetValue<string>();
			if (string.IsNullOrEmpty(tagName))
			{
				return false;
			}

			// Clean version string (remove 'v' prefix if present)
			string cleanVersion = tagName.TrimStart('v', 'V');
			if (!Version.TryParse(cleanVersion, out Version? onlineVersion))
			{
				return false;
			}

			// 3. Compare versions
			if (onlineVersion <= currentVersion)
			{
				return false;
			}

			// 4. Find the SetupScreenRecorder.exe asset download URL
			string? downloadUrl = null;
			JsonArray? assets = releaseNode["assets"]?.AsArray();
			if (assets != null)
			{
				foreach (JsonNode? asset in assets)
				{
					string? name = asset?["name"]?.GetValue<string>();
					if (name != null && name.Equals("SetupScreenRecorder.exe", StringComparison.OrdinalIgnoreCase))
					{
						downloadUrl = asset?["browser_download_url"]?.GetValue<string>();
						break;
					}
				}
			}

			if (string.IsNullOrEmpty(downloadUrl))
			{
				return false;
			}

			// 5. Ask user if we are not in silent check mode (or just proceed)
			if (!silent)
			{
				MessageBoxResult result = MessageBox.Show(
					$"Dostępna jest nowa wersja: {tagName}.\nCzy chcesz ją zainstalować automatycznie?",
					"Aktualizacja",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question
				);
				if (result != MessageBoxResult.Yes)
				{
					return false;
				}
			}

			// 6. Download the installer
			byte[] fileBytes = await Http.GetByteArrayAsync(downloadUrl);
			await File.WriteAllBytesAsync(TempInstallerPath, fileBytes);

			// 7. Cache the changelog and target version
			string? body = releaseNode["body"]?.GetValue<string>();
			if (!string.IsNullOrEmpty(body))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ChangelogCachePath)!);
				await File.WriteAllTextAsync(ChangelogCachePath, body);
			}

			// Save the target version we are updating to
			await File.WriteAllTextAsync(LastVersionFile, onlineVersion.ToString());

			// 8. Run the batch updater and exit
			RunBatchInstallerAndExit();
			return true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Update check error: " + ex.Message);
			if (!silent)
			{
				MessageBox.Show("Błąd podczas sprawdzania aktualizacji:\n" + ex.Message, "Aktualizacja", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			return false;
		}
	}

	private static void RunBatchInstallerAndExit()
	{
		string currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
		string batchPath = Path.Combine(Path.GetTempPath(), "screen_recorder_updater.bat");

		// Write a batch file that waits for the app to exit, installs, and restarts the app
		string batchContent = $@"@echo off
timeout /t 2 /nobreak > nul
start /wait """" ""{TempInstallerPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS
start """" ""{currentExe}""
del ""%~f0""
";

		File.WriteAllText(batchPath, batchContent);

		ProcessStartInfo psi = new ProcessStartInfo
		{
			FileName = "cmd.exe",
			Arguments = $"/c \"{batchPath}\"",
			CreateNoWindow = true,
			UseShellExecute = true
		};

		Process.Start(psi);
		Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
	}
}
