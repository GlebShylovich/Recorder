using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ScreenRecorderApp.Services;

namespace ScreenRecorderApp.ViewModels;

public class SummaryViewModel : INotifyPropertyChanged
{
	public enum Phase
	{
		Compressing,
		Ready,
		Uploading,
		Success,
		Error
	}

	private readonly SessionStateService _sessionState;

	private Phase _currentPhase;

	private double _progress;

	private string _statusText = "Przygotowywanie nagrania…";

	private string _ticketTitle = string.Empty;

	public Phase CurrentPhase
	{
		get
		{
			return _currentPhase;
		}
		private set
		{
			SetField(ref _currentPhase, value, "CurrentPhase");
			OnPropertyChanged("IsCompressing");
			OnPropertyChanged("IsReady");
			OnPropertyChanged("IsUploading");
			OnPropertyChanged("IsSuccess");
			OnPropertyChanged("IsError");
			OnPropertyChanged("CanSubmit");
			OnPropertyChanged("CanRecordAgain");
			SubmitCommand?.RaiseCanExecuteChanged();
			RecordAgainCommand?.RaiseCanExecuteChanged();
			DoneCommand?.RaiseCanExecuteChanged();
		}
	}

	public bool IsCompressing => CurrentPhase == Phase.Compressing;

	public bool IsReady => CurrentPhase == Phase.Ready;

	public bool IsUploading => CurrentPhase == Phase.Uploading;

	public bool IsSuccess => CurrentPhase == Phase.Success;

	public bool IsError => CurrentPhase == Phase.Error;

	public bool CanSubmit
	{
		get
		{
			if (CurrentPhase == Phase.Ready)
			{
				return !string.IsNullOrWhiteSpace(TicketTitle);
			}
			return false;
		}
	}

	public bool CanRecordAgain
	{
		get
		{
			Phase currentPhase = CurrentPhase;
			if (currentPhase == Phase.Ready || currentPhase == Phase.Error)
			{
				return true;
			}
			return false;
		}
	}

	public double Progress
	{
		get
		{
			return _progress;
		}
		set
		{
			SetField(ref _progress, value, "Progress");
		}
	}

	public string StatusText
	{
		get
		{
			return _statusText;
		}
		private set
		{
			SetField(ref _statusText, value, "StatusText");
		}
	}

	public string? VideoPath
	{
		get
		{
			return _sessionState.CurrentVideoPath;
		}
		private set
		{
			_sessionState.CurrentVideoPath = value;
			OnPropertyChanged("VideoPath");
		}
	}

	public string TicketTitle
	{
		get
		{
			return _ticketTitle;
		}
		set
		{
			SetField(ref _ticketTitle, value, "TicketTitle");
			OnPropertyChanged("CanSubmit");
			SubmitCommand?.RaiseCanExecuteChanged();
		}
	}

	public string DescriptionText
	{
		get
		{
			return _sessionState.DescriptionText;
		}
		set
		{
			if (!(_sessionState.DescriptionText == value))
			{
				_sessionState.DescriptionText = value;
				OnPropertyChanged("DescriptionText");
			}
		}
	}

	public RelayCommand SubmitCommand { get; }

	public RelayCommand RecordAgainCommand { get; }

	public RelayCommand DoneCommand { get; }

	public event EventHandler? RecordAgainRequested;

	public event EventHandler? DoneRequested;

	public event EventHandler? VideoReady;

	public event PropertyChangedEventHandler? PropertyChanged;

	public SummaryViewModel(SessionStateService sessionState)
	{
		_sessionState = sessionState;
		SubmitCommand = new RelayCommand(async delegate
		{
			await SubmitAsync();
		}, () => CanSubmit);
		RecordAgainCommand = new RelayCommand(RecordAgain, () => CanRecordAgain);
		DoneCommand = new RelayCommand(delegate
		{
			this.DoneRequested?.Invoke(this, EventArgs.Empty);
		});
	}

	public async Task RunCompressionAsync()
	{
		string currentVideoPath = _sessionState.CurrentVideoPath;
		if (string.IsNullOrEmpty(currentVideoPath) || !File.Exists(currentVideoPath))
		{
			CurrentPhase = Phase.Error;
			StatusText = "Nie znaleziono pliku nagrania.";
			return;
		}
		try
		{
			CurrentPhase = Phase.Compressing;
			Progress = 0.0;
			Progress<(long, long)> onDownloadProgress = new Progress<(long, long)>(delegate((long downloaded, long total) info)
			{
				if (info.total > 0)
				{
					int num = (int)(100.0 * (double)info.downloaded / (double)info.total);
					StatusText = $"Pobieranie FFmpeg… {num}% ({info.downloaded / 1048576} / {info.total / 1048576} MB)";
					Progress = (double)num * 0.5;
				}
			});
			Progress<double> onCompressProgress = new Progress<double>(delegate(double p)
			{
				Progress = 50.0 + p * 50.0;
				StatusText = $"Kompresowanie nagrania… {(int)(p * 100.0)}%";
			});
			StatusText = "Sprawdzanie FFmpeg…";
			VideoPath = await CompressionService.CompressAsync(currentVideoPath, onDownloadProgress, onCompressProgress);
			Progress = 100.0;
			StatusText = "Gotowe";
			CurrentPhase = Phase.Ready;
			this.VideoReady?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			StatusText = "Kompresja nieudana, użyto oryginału. (" + ex.Message + ")";
			CurrentPhase = Phase.Ready;
			this.VideoReady?.Invoke(this, EventArgs.Empty);
		}
	}

	private async Task SubmitAsync()
	{
		if (!CanSubmit)
		{
			return;
		}
		CurrentPhase = Phase.Uploading;
		Progress = 0.0;
		StatusText = "Przesyłanie nagrania…";
		try
		{
			string videoPath = _sessionState.CurrentVideoPath ?? throw new InvalidOperationException("Brak nagrania do przesłania.");
			Progress<double> progress = new Progress<double>(delegate(double p)
			{
				Progress = p * 90.0;
				StatusText = $"Przesyłanie… {(int)(p * 100.0)}%";
			});
			Path.GetFileName(videoPath);
			StatusText = "Przesyłanie do Google Drive…";
			string fileUrl = await GoogleDriveService.UploadVideoAsync(videoPath, progress);
			string fileName = "Google Drive";
			StatusText = "Tworzenie zgłoszenia…";
			Progress = 92.0;
			string ticketId = await SupabaseService.CreateTicketAsync(TicketTitle, DescriptionText);
			StatusText = "Zapisywanie załącznika…";
			Progress = 97.0;
			await SupabaseService.CreateAttachmentAsync(ticketId, fileUrl, fileName);
			TryDeleteFile(videoPath);
			VideoPath = null;
			Progress = 100.0;
			StatusText = "Zgłoszenie wysłane pomyślnie";
			CurrentPhase = Phase.Success;
		}
		catch (Exception ex)
		{
			StatusText = "Błąd: " + ex.Message;
			CurrentPhase = Phase.Error;
		}
	}

	private void RecordAgain()
	{
		TryDeleteFile(_sessionState.CurrentVideoPath);
		VideoPath = null;
		_sessionState.DescriptionText = string.Empty;
		TicketTitle = string.Empty;
		CurrentPhase = Phase.Ready;
		Progress = 0.0;
		StatusText = string.Empty;
		this.RecordAgainRequested?.Invoke(this, EventArgs.Empty);
	}

	private static void TryDeleteFile(string? path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return;
		}
		try
		{
			File.Delete(path);
		}
		catch
		{
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
}
