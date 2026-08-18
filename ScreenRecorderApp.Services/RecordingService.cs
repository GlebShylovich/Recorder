using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ScreenRecorderLib;

namespace ScreenRecorderApp.Services;

public class RecordingService : IDisposable
{
	private Recorder? _recorder;

	private bool _isMicMuted;

	private bool _useHardwareEncoding = true;

	private List<nint>? _excludedHandles;

	private string? _lastOutputPath;

	private string? _lastAudioDevice;

	private Rect? _lastRegion;

	private List<nint>? _lastExcludedHandles;

	public bool IsRecording
	{
		get
		{
			Recorder recorder = _recorder;
			if (recorder == null)
			{
				return false;
			}
			return recorder.Status == RecorderStatus.Recording;
		}
	}

	public event EventHandler<string>? RecordingCompleted;

	public event EventHandler<string>? RecordingFailed;

	public static string CreateOutputFilePath()
	{
		DateTime now = DateTime.Now;
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "Recordings");
		Directory.CreateDirectory(text);
		return Path.Combine(text, $"recording_{now:yyyy-MM-dd_HHmmss}.mp4");
	}

	public void StartRecording(string outputFilePath, string? audioInputDeviceId = null, Rect? region = null, IEnumerable<nint>? excludedWindowHandles = null)
	{
		if (!IsRecording)
		{
			string directoryName = Path.GetDirectoryName(outputFilePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			if (new DriveInfo(Path.GetPathRoot(outputFilePath) ?? "C:\\").AvailableFreeSpace < 524288000)
			{
				throw new IOException("Za mało miejsca na dysku. Wymagane minimum 500 MB wolnego miejsca.");
			}
			_lastOutputPath = outputFilePath;
			_lastAudioDevice = audioInputDeviceId;
			_lastRegion = region;
			_lastExcludedHandles = ((excludedWindowHandles != null) ? new List<nint>(excludedWindowHandles) : null);
			StartRecordingInternal(outputFilePath, audioInputDeviceId, region, excludedWindowHandles, _useHardwareEncoding);
		}
	}

	private void StartRecordingInternal(string outputFilePath, string? audioInputDeviceId, Rect? region, IEnumerable<nint>? excludedWindowHandles, bool hardwareEncoding)
	{
		List<RecordingSourceBase> list = new List<RecordingSourceBase>();
		if (region.HasValue)
		{
			DisplayRecordingSource mainMonitor = DisplayRecordingSource.MainMonitor;
			mainMonitor.SourceRect = new ScreenRect((int)region.Value.X, (int)region.Value.Y, (int)region.Value.Width, (int)region.Value.Height);
			list.Add(mainMonitor);
		}
		else
		{
			list.Add(DisplayRecordingSource.MainMonitor);
		}
		string audioInputDevice = null;
		if (!string.IsNullOrEmpty(audioInputDeviceId))
		{
			audioInputDevice = audioInputDeviceId;
		}
		RecorderOptions options = new RecorderOptions
		{
			SourceOptions = new SourceOptions
			{
				RecordingSources = list
			},
			OutputOptions = new OutputOptions
			{
				RecorderMode = RecorderMode.Video
			},
			VideoEncoderOptions = new VideoEncoderOptions
			{
				Encoder = new H264VideoEncoder
				{
					BitrateMode = H264BitrateControlMode.CBR,
					EncoderProfile = H264Profile.Main
				},
				Bitrate = 2500000,
				Framerate = 24,
				IsHardwareEncodingEnabled = hardwareEncoding
			},
			AudioOptions = new AudioOptions
			{
				IsAudioEnabled = true,
				IsInputDeviceEnabled = true,
				IsOutputDeviceEnabled = false,
				AudioInputDevice = audioInputDevice,
				InputVolume = (_isMicMuted ? 0f : 1f)
			}
		};
		_recorder = Recorder.CreateRecorder(options);
		_recorder.OnRecordingComplete += OnRecordingComplete;
		_recorder.OnRecordingFailed += OnRecordingFailed;
		if (excludedWindowHandles != null)
		{
			foreach (nint excludedWindowHandle in excludedWindowHandles)
			{
				Recorder.SetExcludeFromCapture(excludedWindowHandle, isExcluded: true);
			}
			_excludedHandles = new List<nint>(excludedWindowHandles);
		}
		_recorder.Record(outputFilePath);
	}

	public void StopRecording()
	{
		_recorder?.Stop();
	}

	public void SetMicrophoneMuted(bool isMuted)
	{
		_isMicMuted = isMuted;
		if (_recorder != null && IsRecording)
		{
			_recorder.GetDynamicOptionsBuilder().SetDynamicAudioOptions(new DynamicAudioOptions
			{
				InputVolume = (isMuted ? 0f : 1f)
			}).Apply();
		}
	}

	private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
	{
		this.RecordingCompleted?.Invoke(this, e.FilePath);
		DisposeRecorder();
	}

	private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
	{
		bool num = e.Error != null && (e.Error.Contains("0xc00d36bb", StringComparison.OrdinalIgnoreCase) || e.Error.Contains("video encoder", StringComparison.OrdinalIgnoreCase) || e.Error.Contains("hardware encod", StringComparison.OrdinalIgnoreCase));
		DisposeRecorder();
		if (num && _useHardwareEncoding && _lastOutputPath != null)
		{
			_useHardwareEncoding = false;
			string outputPath = _lastOutputPath;
			string audioDevice = _lastAudioDevice;
			Rect? region = _lastRegion;
			List<nint> excludedHandles = ((_lastExcludedHandles != null) ? new List<nint>(_lastExcludedHandles) : null);
			if (File.Exists(outputPath))
			{
				try
				{
					File.Delete(outputPath);
				}
				catch
				{
				}
			}
			Application.Current?.Dispatcher.BeginInvoke((Action)delegate
			{
				try
				{
					StartRecordingInternal(outputPath, audioDevice, region, excludedHandles, hardwareEncoding: false);
				}
				catch (Exception ex)
				{
					this.RecordingFailed?.Invoke(this, ex.Message);
				}
			});
		}
		else
		{
			this.RecordingFailed?.Invoke(this, e.Error ?? "Unknown error");
		}
	}

	private void DisposeRecorder()
	{
		if (_recorder == null)
		{
			return;
		}
		_recorder.OnRecordingComplete -= OnRecordingComplete;
		_recorder.OnRecordingFailed -= OnRecordingFailed;
		if (_excludedHandles != null)
		{
			foreach (nint excludedHandle in _excludedHandles)
			{
				Recorder.SetExcludeFromCapture(excludedHandle, isExcluded: false);
			}
			_excludedHandles = null;
		}
		_recorder.Dispose();
		_recorder = null;
	}

	public void Dispose()
	{
		DisposeRecorder();
		GC.SuppressFinalize(this);
	}
}
