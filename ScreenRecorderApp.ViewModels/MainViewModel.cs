using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScreenRecorderApp.Services;
using ScreenRecorderApp.Views;

namespace ScreenRecorderApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
	public enum RecordingMode
	{
		FullScreen,
		Region
	}

	private readonly RecordingService _recordingService = new RecordingService();

	private readonly DispatcherTimer _elapsedTimer;

	private DateTime _recordingStartedAt;

	private bool _isRecording;

	private string _elapsedTime = "00:00";

	private bool _isMuted;

	private AudioDeviceService.MicrophoneDevice? _selectedMicrophone;

	private const int MaxRecordingMinutes = 10;

	private RecordingMode _mode;

	private Rect? _selectedRegion;

	private nint _ownWindowHandle;

	public bool IsRecording
	{
		get
		{
			return _isRecording;
		}
		private set
		{
			if (SetField(ref _isRecording, value, "IsRecording"))
			{
				OnPropertyChanged("RecordingTooltip");
				OnPropertyChanged("IsNotRecording");
				CommandManager.InvalidateRequerySuggested();
			}
		}
	}

	public string RecordingTooltip
	{
		get
		{
			if (!IsRecording)
			{
				return "Rozpocznij nagrywanie";
			}
			return "Zakończ nagrywanie";
		}
	}

	public string ElapsedTime
	{
		get
		{
			return _elapsedTime;
		}
		private set
		{
			SetField(ref _elapsedTime, value, "ElapsedTime");
		}
	}

	public bool IsMuted
	{
		get
		{
			return _isMuted;
		}
		private set
		{
			if (SetField(ref _isMuted, value, "IsMuted"))
			{
				OnPropertyChanged("MuteTooltip");
			}
		}
	}

	public string MuteTooltip
	{
		get
		{
			if (!IsMuted)
			{
				return "Wycisz mikrofon";
			}
			return "Włącz mikrofon";
		}
	}

	public IReadOnlyList<AudioDeviceService.MicrophoneDevice> Microphones { get; }

	public AudioDeviceService.MicrophoneDevice? SelectedMicrophone
	{
		get
		{
			return _selectedMicrophone;
		}
		set
		{
			SetField(ref _selectedMicrophone, value, "SelectedMicrophone");
		}
	}

	public RecordingMode Mode
	{
		get
		{
			return _mode;
		}
		set
		{
			if (SetField(ref _mode, value, "Mode"))
			{
				OnPropertyChanged("IsFullScreenMode");
				OnPropertyChanged("IsRegionMode");
			}
		}
	}

	public bool IsFullScreenMode => Mode == RecordingMode.FullScreen;

	public bool IsRegionMode => Mode == RecordingMode.Region;

	public bool IsNotRecording => !IsRecording;

	public RelayCommand ToggleRecordingCommand { get; }

	public RelayCommand ToggleMuteCommand { get; }

	public RelayCommand CloseCommand { get; }

	public RelayCommand LogoutCommand { get; }

	public string ClientName => SupabaseService.ClientName ?? "Użytkownik";

	public RelayCommand PickRegionCommand { get; }

	public RelayCommand SetFullScreenCommand { get; }

	public SessionStateService SessionState { get; } = new SessionStateService();

	public event EventHandler<string>? RecordingSaved;

	public event PropertyChangedEventHandler? PropertyChanged;

	public MainViewModel()
	{
		Microphones = AudioDeviceService.GetMicrophones();
		_selectedMicrophone = ((Microphones.Count > 0) ? Microphones[0] : null);
		ToggleRecordingCommand = new RelayCommand(ToggleRecording);
		ToggleMuteCommand = new RelayCommand(ToggleMute);
		CloseCommand = new RelayCommand(CloseApplication);
		LogoutCommand = new RelayCommand(Logout, () => !IsRecording);
		PickRegionCommand = new RelayCommand(PickRegion, () => !IsRecording);
		SetFullScreenCommand = new RelayCommand(delegate
		{
			Mode = RecordingMode.FullScreen;
			_selectedRegion = null;
		}, () => !IsRecording);
		_elapsedTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1L)
		};
		_elapsedTimer.Tick += delegate
		{
			TimeSpan timeSpan = DateTime.Now - _recordingStartedAt;
			ElapsedTime = timeSpan.ToString("mm\\:ss");
			if (timeSpan.TotalMinutes >= 10.0)
			{
				_recordingService.StopRecording();
				ElapsedTime = $"{10:00}:00";
			}
		};
		_recordingService.RecordingCompleted += delegate(object? _, string path)
		{
			Application.Current.Dispatcher.Invoke(delegate
			{
				HandleRecordingCompleted(path);
			});
		};
		_recordingService.RecordingFailed += delegate(object? _, string err)
		{
			Application.Current.Dispatcher.Invoke(delegate
			{
				HandleRecordingFailed(err);
			});
		};
	}

	public void SetOwnWindowHandle(nint handle)
	{
		_ownWindowHandle = handle;
	}

	private void ToggleRecording()
	{
		if (IsRecording)
		{
			_recordingService.StopRecording();
			return;
		}
		try
		{
			string outputFilePath = RecordingService.CreateOutputFilePath();
			string audioInputDeviceId = (string.IsNullOrEmpty(SelectedMicrophone?.Id) ? null : SelectedMicrophone.Id);
			_recordingService.StartRecording(outputFilePath, audioInputDeviceId, _selectedRegion, new nint[1] { _ownWindowHandle });
			_recordingStartedAt = DateTime.Now;
			ElapsedTime = "00:00";
			_elapsedTimer.Start();
			IsRecording = true;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Błąd nagrywania", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void ToggleMute()
	{
		IsMuted = !IsMuted;
		_recordingService.SetMicrophoneMuted(IsMuted);
	}

	private void PickRegion()
	{
		Application.Current.MainWindow?.Hide();
		RegionPickerWindow regionPickerWindow = new RegionPickerWindow();
		regionPickerWindow.ShowDialog();
		Application.Current.MainWindow?.Show();
		if (regionPickerWindow.SelectedRect.HasValue)
		{
			_selectedRegion = regionPickerWindow.SelectedRect;
			Mode = RecordingMode.Region;
		}
	}

	private void HandleRecordingCompleted(string filePath)
	{
		_elapsedTimer.Stop();
		IsRecording = false;
		SessionState.CurrentVideoPath = filePath;
		this.RecordingSaved?.Invoke(this, filePath);
	}

	private void HandleRecordingFailed(string error)
	{
		_elapsedTimer.Stop();
		IsRecording = false;
		MessageBox.Show("Nagrywanie nie powiodło się:\n" + error, "Błąd", MessageBoxButton.OK, MessageBoxImage.Hand);
	}

	private void CloseApplication()
	{
		Environment.Exit(0);
	}

	private void Logout()
	{
		SupabaseService.SignOut();
		LoginWindow loginWindow = new LoginWindow();
		loginWindow.Show();
		foreach (Window item in Application.Current.Windows.Cast<Window>().ToList())
		{
			if (item != loginWindow)
			{
				item.Close();
			}
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
		_elapsedTimer.Stop();
		_recordingService.Dispose();
	}
}
