using System;
using System.Collections.Generic;
using ScreenRecorderLib;

namespace ScreenRecorderApp.Services;

public static class AudioDeviceService
{
	public record MicrophoneDevice(string Id, string Name, string FriendlyName);

	public static IReadOnlyList<MicrophoneDevice> GetMicrophones()
	{
		List<MicrophoneDevice> list = new List<MicrophoneDevice>
		{
			new MicrophoneDevice(string.Empty, "System default", "System default")
		};
		try
		{
			foreach (AudioDevice systemAudioDevice in Recorder.GetSystemAudioDevices(AudioDeviceSource.InputDevices))
			{
				list.Add(new MicrophoneDevice(systemAudioDevice.DeviceName, systemAudioDevice.FriendlyName, systemAudioDevice.FriendlyName));
			}
		}
		catch (Exception)
		{
		}
		return list;
	}
}
