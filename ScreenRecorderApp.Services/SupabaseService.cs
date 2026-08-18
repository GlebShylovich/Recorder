using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenRecorderApp.Services;

public static class SupabaseService
{
	private const string SupabaseUrl = "https://wdsgtbdqgtwnywvkquhd.supabase.co";

	private const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indkc2d0YmRxZ3R3bnl3dmtxdWhkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM4MjgyMjEsImV4cCI6MjA4OTQwNDIyMX0.jDOtnSauyl-r6WS-XUJm-uQD0-8Tw31l_IovsO2Lfps";

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromMinutes(10L)
	};

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};

	public static string? AccessToken { get; private set; }

	public static string? RefreshToken { get; private set; }

	public static string? UserId { get; private set; }

	public static string? ClientId { get; private set; }

	public static string? ClientName { get; private set; }

	public static string? UserEmail { get; private set; }

	public static bool IsSignedIn => AccessToken != null;

	private static string SessionFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "session.json");

	public static async Task SignInAsync(string email, string password)
	{
		string content = JsonSerializer.Serialize(new { email, password });
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "https://wdsgtbdqgtwnywvkquhd.supabase.co/auth/v1/token?grant_type=password");
		req.Headers.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indkc2d0YmRxZ3R3bnl3dmtxdWhkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM4MjgyMjEsImV4cCI6MjA4OTQwNDIyMX0.jDOtnSauyl-r6WS-XUJm-uQD0-8Tw31l_IovsO2Lfps");
		req.Content = new StringContent(content, Encoding.UTF8, "application/json");
		using HttpResponseMessage res = await Http.SendAsync(req);
		string json = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ExtractError(json));
		}
		JsonNode? jsonNode = JsonNode.Parse(json);
		AccessToken = jsonNode?["access_token"]?.GetValue<string>();
		RefreshToken = jsonNode?["refresh_token"]?.GetValue<string>();
		UserEmail = email;
		UserId = jsonNode?["user"]?["id"]?.GetValue<string>();
		await LoadClientAsync(email);
		SaveSession();
	}

	public static void SignOut()
	{
		AccessToken = (RefreshToken = (UserId = (ClientId = (ClientName = (UserEmail = null)))));
		GoogleDriveService.SignOut();
		try
		{
			if (File.Exists(SessionFilePath))
			{
				File.Delete(SessionFilePath);
			}
		}
		catch
		{
		}
	}

	public static void SaveSession()
	{
		try
		{
			var value = new { AccessToken, RefreshToken, UserId, ClientId, ClientName, UserEmail };
			string directoryName = Path.GetDirectoryName(SessionFilePath);
			if (directoryName != null)
			{
				Directory.CreateDirectory(directoryName);
			}
			byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOpts)), null, DataProtectionScope.CurrentUser);
			File.WriteAllBytes(SessionFilePath, bytes);
		}
		catch
		{
		}
	}

	public static bool TryLoadSession()
	{
		try
		{
			if (File.Exists(SessionFilePath))
			{
				byte[] encryptedData = File.ReadAllBytes(SessionFilePath);
				byte[] bytes;
				try
				{
					bytes = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
				}
				catch
				{
					File.Delete(SessionFilePath);
					return false;
				}
				JsonNode jsonNode = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
				if (jsonNode != null)
				{
					AccessToken = jsonNode["access_token"]?.GetValue<string>();
					RefreshToken = jsonNode["refresh_token"]?.GetValue<string>();
					UserId = jsonNode["user_id"]?.GetValue<string>();
					ClientId = jsonNode["client_id"]?.GetValue<string>();
					ClientName = jsonNode["client_name"]?.GetValue<string>();
					UserEmail = jsonNode["user_email"]?.GetValue<string>();
					if (AccessToken != null)
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool IsTokenExpired()
	{
		if (string.IsNullOrEmpty(AccessToken))
		{
			return true;
		}
		try
		{
			string[] array = AccessToken.Split('.');
			if (array.Length < 2)
			{
				return true;
			}
			string text = array[1].Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
			case 2:
				text += "==";
				break;
			case 3:
				text += "=";
				break;
			}
			JsonNode jsonNode = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(text)));
			if (jsonNode != null && jsonNode["exp"] != null)
			{
				DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(jsonNode["exp"].GetValue<long>());
				return DateTimeOffset.UtcNow >= dateTimeOffset.AddMinutes(-5.0);
			}
		}
		catch
		{
		}
		return false;
	}

	public static async Task RefreshSessionAsync()
	{
		if (string.IsNullOrEmpty(RefreshToken))
		{
			SignOut();
			throw new InvalidOperationException("Sesja wygasła. Zaloguj się ponownie.");
		}
		string content = JsonSerializer.Serialize(new
		{
			refresh_token = RefreshToken
		});
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "https://wdsgtbdqgtwnywvkquhd.supabase.co/auth/v1/token?grant_type=refresh_token");
		req.Headers.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indkc2d0YmRxZ3R3bnl3dmtxdWhkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM4MjgyMjEsImV4cCI6MjA4OTQwNDIyMX0.jDOtnSauyl-r6WS-XUJm-uQD0-8Tw31l_IovsO2Lfps");
		req.Content = new StringContent(content, Encoding.UTF8, "application/json");
		using HttpResponseMessage res = await Http.SendAsync(req);
		string json = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
		{
			SignOut();
			throw new InvalidOperationException("Sesja wygasła. Zaloguj się ponownie.");
		}
		JsonNode? jsonNode = JsonNode.Parse(json);
		AccessToken = jsonNode["access_token"]?.GetValue<string>();
		RefreshToken = jsonNode["refresh_token"]?.GetValue<string>();
		SaveSession();
	}

	public static async Task EnsureAuthenticatedAsync()
	{
		if (AccessToken == null)
		{
			throw new InvalidOperationException("Nie zalogowano.");
		}
		if (IsTokenExpired())
		{
			await RefreshSessionAsync();
		}
	}

	private static async Task LoadClientAsync(string email)
	{
		using HttpRequestMessage req2 = BuildRequest(HttpMethod.Get, "https://wdsgtbdqgtwnywvkquhd.supabase.co/rest/v1/profiles?email=eq." + Uri.EscapeDataString(email) + "&select=full_name,client_id&limit=1");
		req2.Headers.Add("Accept", "application/json");
		using HttpResponseMessage res2 = await Http.SendAsync(req2);
		if (!res2.IsSuccessStatusCode)
		{
			return;
		}
		JsonArray jsonArray = JsonNode.Parse(await res2.Content.ReadAsStringAsync())?.AsArray();
		if (jsonArray != null && jsonArray.Count > 0)
		{
			JsonNode jsonNode = jsonArray[0]?["client_id"];
			if (jsonNode != null)
			{
				ClientId = jsonNode.GetValue<string>();
			}
			ClientName = jsonArray[0]?["full_name"]?.GetValue<string>();
		}
	}

	public static async Task<string> CreateTicketAsync(string title, string description, CancellationToken ct = default(CancellationToken))
	{
		await EnsureAuthenticatedAsync();
		var value = new
		{
			title = title,
			description = description,
			client_id = ClientId,
			guest_email = UserEmail,
			source = "app",
			department = "Zgłoszenia problemów",
			status = "NOWE",
			priority = "Średni"
		};
		string jsonPayload = JsonSerializer.Serialize(value, JsonOpts);
		for (int i = 0; i < 2; i++)
		{
			using HttpRequestMessage req = BuildRequest(HttpMethod.Post, "https://wdsgtbdqgtwnywvkquhd.supabase.co/rest/v1/tickets");
			req.Headers.Add("Prefer", "return=representation");
			req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
			using HttpResponseMessage res = await Http.SendAsync(req, ct);
			string text = await res.Content.ReadAsStringAsync(ct);
			if (!res.IsSuccessStatusCode)
			{
				if (i != 0 || (res.StatusCode != HttpStatusCode.Unauthorized && !text.Contains("exp claim timestamp check failed")))
				{
					throw new InvalidOperationException("Ticket creation failed: " + ExtractError(text));
				}
				await RefreshSessionAsync();
				continue;
			}
			return (JsonNode.Parse(text)?.AsArray())?[0]?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("No ticket ID returned.");
		}
		throw new InvalidOperationException("Ticket creation failed.");
	}

	public static async Task CreateAttachmentAsync(string ticketId, string fileUrl, string fileName, CancellationToken ct = default(CancellationToken))
	{
		await EnsureAuthenticatedAsync();
		var value = new
		{
			ticket_id = ticketId,
			file_url = fileUrl,
			file_name = fileName
		};
		string jsonPayload = JsonSerializer.Serialize(value, JsonOpts);
		for (int i = 0; i < 2; i++)
		{
			using HttpRequestMessage req = BuildRequest(HttpMethod.Post, "https://wdsgtbdqgtwnywvkquhd.supabase.co/rest/v1/ticket_attachments");
			req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
			using HttpResponseMessage res = await Http.SendAsync(req, ct);
			string text = await res.Content.ReadAsStringAsync(ct);
			if (!res.IsSuccessStatusCode)
			{
				if (i != 0 || (res.StatusCode != HttpStatusCode.Unauthorized && !text.Contains("exp claim timestamp check failed")))
				{
					throw new InvalidOperationException("Attachment creation failed: " + ExtractError(text));
				}
				await RefreshSessionAsync();
				continue;
			}
			break;
		}
	}

	private static HttpRequestMessage BuildRequest(HttpMethod method, string url)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(method, url);
		httpRequestMessage.Headers.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indkc2d0YmRxZ3R3bnl3dmtxdWhkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM4MjgyMjEsImV4cCI6MjA4OTQwNDIyMX0.jDOtnSauyl-r6WS-XUJm-uQD0-8Tw31l_IovsO2Lfps");
		if (AccessToken != null)
		{
			httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
		}
		return httpRequestMessage;
	}

	private static string ExtractError(string json)
	{
		try
		{
			JsonNode jsonNode = JsonNode.Parse(json);
			return jsonNode?["message"]?.GetValue<string>() ?? jsonNode?["error_description"]?.GetValue<string>() ?? json;
		}
		catch
		{
			return json;
		}
	}
}
