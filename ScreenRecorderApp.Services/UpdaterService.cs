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

	private static string TempInstallerPath => Path.Combine(Path.GetTempPath(), "SetupEManagerPomoc_Update.exe");
	public static string ChangelogCachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMANAGER Pomoc", "changelog_cache.txt");
	public static string LastVersionFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMANAGER Pomoc", "last_version.txt");

	public class UpdateInfo
	{
		public bool IsUpdateAvailable { get; set; }
		public string VersionTag { get; set; } = string.Empty;
		public string DownloadUrl { get; set; } = string.Empty;
		public string ChangelogText { get; set; } = string.Empty;
		public Version OnlineVersion { get; set; } = new Version(1, 0, 0, 0);
	}

	static UpdaterService()
	{
		Http.DefaultRequestHeaders.Add("User-Agent", "ScreenRecorderApp-Updater");
	}

	public static async Task<UpdateInfo> CheckForUpdateInfoAsync()
	{
		UpdateInfo info = new UpdateInfo();
		try
		{
			// 1. Get current assembly version
			Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

			// 2. Fetch latest release from GitHub
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, RepoUrl);
			using HttpResponseMessage res = await Http.SendAsync(req);
			if (!res.IsSuccessStatusCode)
			{
				return info;
			}

			string json = await res.Content.ReadAsStringAsync();
			JsonNode? releaseNode = JsonNode.Parse(json);
			if (releaseNode == null)
			{
				return info;
			}

			string? tagName = releaseNode["tag_name"]?.GetValue<string>();
			if (string.IsNullOrEmpty(tagName))
			{
				return info;
			}

			// Clean version string (remove 'v' prefix if present)
			string cleanVersion = tagName.TrimStart('v', 'V');
			if (!cleanVersion.Contains("."))
			{
				cleanVersion += ".0";
			}
			if (!Version.TryParse(cleanVersion, out Version? onlineVersion))
			{
				return info;
			}

			// 3. Compare versions
			if (onlineVersion <= currentVersion)
			{
				return info;
			}

			// 4. Find the SetupEManagerPomoc.exe asset download URL
			string? downloadUrl = null;
			JsonArray? assets = releaseNode["assets"]?.AsArray();
			if (assets != null)
			{
				foreach (JsonNode? asset in assets)
				{
					string? name = asset?["name"]?.GetValue<string>();
					if (name != null && name.Equals("SetupEManagerPomoc.exe", StringComparison.OrdinalIgnoreCase))
					{
						downloadUrl = asset?["browser_download_url"]?.GetValue<string>();
						break;
					}
				}
			}

			if (string.IsNullOrEmpty(downloadUrl))
			{
				return info;
			}

			info.IsUpdateAvailable = true;
			info.VersionTag = tagName;
			info.DownloadUrl = downloadUrl;
			info.ChangelogText = releaseNode["body"]?.GetValue<string>() ?? string.Empty;
			info.OnlineVersion = onlineVersion;
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Update check error: " + ex.Message);
		}
		return info;
	}

	public static async Task<bool> DownloadAndInstallAsync(UpdateInfo info)
	{
		try
		{
			// 1. Download the installer
			byte[] fileBytes = await Http.GetByteArrayAsync(info.DownloadUrl);
			await File.WriteAllBytesAsync(TempInstallerPath, fileBytes);

			// 2. Cache the changelog and target version
			if (!string.IsNullOrEmpty(info.ChangelogText))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ChangelogCachePath)!);
				await File.WriteAllTextAsync(ChangelogCachePath, info.ChangelogText);
			}

			// Save the target version we are updating to
			await File.WriteAllTextAsync(LastVersionFile, info.OnlineVersion.ToString());

			// 3. Run the batch installer and exit
			RunBatchInstallerAndExit();
			return true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Download/Install error: " + ex.Message);
			return false;
		}
	}

	public static async Task<bool> CheckAndPerformUpdateAsync(bool silent = true)
	{
		UpdateInfo info = await CheckForUpdateInfoAsync();
		if (!info.IsUpdateAvailable)
		{
			return false;
		}

		if (!silent)
		{
			MessageBoxResult result = MessageBox.Show(
				$"Dostępna jest nowa wersja: {info.VersionTag}.\nCzy chcesz ją zainstalować automatycznie?",
				"Aktualizacja",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question
			);
			if (result != MessageBoxResult.Yes)
			{
				return false;
			}
		}

		return await DownloadAndInstallAsync(info);
	}

	private static void RunBatchInstallerAndExit()
	{
		string currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
		string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EMANAGER Pomoc");
		string installedExe = Path.Combine(appDataFolder, "EManagerPomoc.exe");
		string batchPath = Path.Combine(Path.GetTempPath(), "screen_recorder_updater.bat");

		// Write a batch file that checks if the installed app exists after setup, and if so launches it
		string batchContent = $@"@echo off
timeout /t 2 /nobreak > nul
start /wait """" ""{TempInstallerPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS
if exist ""{installedExe}"" (
    start """" ""{installedExe}""
) else (
    start """" ""{currentExe}""
)
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
