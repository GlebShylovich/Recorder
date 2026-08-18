using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ScreenRecorderApp.Services;

namespace ScreenRecorderApp.ViewModels;

public class LoginViewModel : INotifyPropertyChanged, IDisposable
{
	private string _email = string.Empty;

	private string _password = string.Empty;

	private string? _errorMessage;

	private bool _isBusy;

	public string Email
	{
		get
		{
			return _email;
		}
		set
		{
			SetField(ref _email, value, "Email");
			OnPropertyChanged("CanLogin");
		}
	}

	public string Password
	{
		get
		{
			return _password;
		}
		set
		{
			SetField(ref _password, value, "Password");
			OnPropertyChanged("CanLogin");
		}
	}

	public string? ErrorMessage
	{
		get
		{
			return _errorMessage;
		}
		private set
		{
			SetField(ref _errorMessage, value, "ErrorMessage");
		}
	}

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		private set
		{
			SetField(ref _isBusy, value, "IsBusy");
			OnPropertyChanged("CanLogin");
		}
	}

	public bool CanLogin
	{
		get
		{
			if (!IsBusy && !string.IsNullOrWhiteSpace(Email))
			{
				return !string.IsNullOrWhiteSpace(Password);
			}
			return false;
		}
	}

	public RelayCommand LoginCommand { get; }

	public event EventHandler? LoginSucceeded;

	public event PropertyChangedEventHandler? PropertyChanged;

	public LoginViewModel()
	{
		LoginCommand = new RelayCommand(async delegate
		{
			await LoginAsync();
		}, () => CanLogin);
	}

	private async Task LoginAsync()
	{
		if (!CanLogin)
		{
			return;
		}
		IsBusy = true;
		ErrorMessage = null;
		try
		{
			await SupabaseService.SignInAsync(Email.Trim(), Password);
			if (!SupabaseService.IsSignedIn)
			{
				ErrorMessage = "Nie znaleziono klienta powiązanego z tym adresem e-mail.";
			}
			else
			{
				this.LoginSucceeded?.Invoke(this, EventArgs.Empty);
			}
		}
		catch (Exception ex)
		{
			ErrorMessage = (ex.Message.Contains("Invalid login", StringComparison.OrdinalIgnoreCase) ? "Nieprawidłowy adres e-mail lub hasło." : ex.Message);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (object.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void Dispose()
	{
	}
}
