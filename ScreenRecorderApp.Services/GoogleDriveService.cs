using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Upload;

namespace ScreenRecorderApp.Services;

public static class GoogleDriveService
{
	private static readonly string[] Scopes = new string[1] { DriveService.Scope.DriveFile };

	private const string ApplicationName = "ScreenRecorderApp";

	private const string TargetFolderId = "1EhOqxk2U_tp2Vy3-mzUemNEsBt3s-crF";

	public static bool IsLinked => true;

	public static void SignOut()
	{
	}

	public static Task<string> UploadVideoAsync(string filePath, IProgress<double>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		string rt = Decode("MS8vMDMzdlNGOXRlTmNIS0NnWUlBUkFBR0FNU053Ri1MOUlyV25QckhTdzFlbzd6U1dMOXA2alNadk9FZXhweG5yQXJJenBUN1NwU1dZenpUOVpRSUxJUTQ0cnA4QkZMRUROUGthTQ==");
		string cid = Decode("OTg5NjAwODcwNjU4LTZpMW45dTNtb3VjMzloNnQ3NGhkamRpZTNta2V2MGhqLmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29t");
		string cs = Decode("R09DU1BYLWVITFNYMDVDcWhDTXR6YUNObVRXSmZVR3RDS08=");
		return UploadVideoInternalAsync(filePath, rt, cid, cs, progress, ct);
	}

	private static string Decode(string b64)
	{
		return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
	}

	private static async Task<string> UploadVideoInternalAsync(string filePath, string rt, string cid, string cs, IProgress<double>? progress, CancellationToken ct)
	{
		TokenResponse token = new TokenResponse
		{
			RefreshToken = rt
		};
		UserCredential httpClientInitializer = new UserCredential(new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
		{
			ClientSecrets = new ClientSecrets
			{
				ClientId = cid,
				ClientSecret = cs
			},
			Scopes = Scopes
		}), "user", token);
		DriveService service = new DriveService(new BaseClientService.Initializer
		{
			HttpClientInitializer = httpClientInitializer,
			ApplicationName = "ScreenRecorderApp"
		});
		Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File
		{
			Name = Path.GetFileName(filePath),
			MimeType = "video/mp4",
			Parents = new List<string> { "1EhOqxk2U_tp2Vy3-mzUemNEsBt3s-crF" }
		};
		FileStream stream = new FileStream(filePath, FileMode.Open);
		FilesResource.CreateMediaUpload request;
		try
		{
			request = service.Files.Create(body, stream, "video/mp4");
			request.Fields = "id, webViewLink";
			request.SupportsAllDrives = true;
			if (progress != null)
			{
				request.ProgressChanged += delegate(IUploadProgress p)
				{
					if (p.Status == UploadStatus.Uploading && stream.Length > 0)
					{
						progress.Report((double)p.BytesSent / (double)stream.Length);
					}
				};
			}
			IUploadProgress uploadProgress = await request.UploadAsync(ct);
			if (uploadProgress.Status == UploadStatus.Failed)
			{
				throw new Exception("Google Drive upload failed: " + uploadProgress.Exception?.Message);
			}
		}
		finally
		{
			if (stream != null)
			{
				((IDisposable)stream).Dispose();
			}
		}
		Google.Apis.Drive.v3.Data.File file = request.ResponseBody;
		Permission body2 = new Permission
		{
			Type = "anyone",
			Role = "reader"
		};
		await service.Permissions.Create(body2, file.Id).ExecuteAsync(ct);
		return file.WebViewLink;
	}
}
