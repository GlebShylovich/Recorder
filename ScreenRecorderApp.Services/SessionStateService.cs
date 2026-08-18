using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenRecorderApp.Services;

public class SessionStateService : INotifyPropertyChanged
{
	private string? _currentVideoPath;

	private string _descriptionText = string.Empty;

	public string? CurrentVideoPath
	{
		get
		{
			return _currentVideoPath;
		}
		set
		{
			SetField(ref _currentVideoPath, value, "CurrentVideoPath");
		}
	}

	public string DescriptionText
	{
		get
		{
			return _descriptionText;
		}
		set
		{
			SetField(ref _descriptionText, value, "DescriptionText");
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (object.Equals(field, value))
		{
			return false;
		}
		field = value;
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}
