namespace HearEqualOrNah.Audio.Devices;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Microsoft.Extensions.Logging;

public sealed class AudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly ILogger<AudioDeviceService> _logger;

    public event Action? DevicesChanged
    {
        add { }
        remove { }
    }

    public AudioDeviceService(ILogger<AudioDeviceService> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
    }

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        try
        {
            var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var device in collection)
            {
                devices.Add(CreateDeviceInfo(device, true));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate input devices");
        }

        return devices;
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        try
        {
            var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in collection)
            {
                devices.Add(CreateDeviceInfo(device, false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate output devices");
        }

        return devices;
    }

    public AudioDeviceInfo? GetDefaultInputDevice()
    {
        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            return CreateDeviceInfo(device, true);
        }
        catch
        {
            return null;
        }
    }

    public AudioDeviceInfo? GetDefaultOutputDevice()
    {
        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return CreateDeviceInfo(device, false);
        }
        catch
        {
            return null;
        }
    }

    public bool IsDeviceAvailable(string deviceId, bool isInput)
    {
        try
        {
            var device = _enumerator.GetDevice(deviceId);
            return device.State == DeviceState.Active;
        }
        catch
        {
            return false;
        }
    }

    private AudioDeviceInfo CreateDeviceInfo(MMDevice device, bool isInput)
    {
        var type = DetermineDeviceType(device);
        var channels = GetChannelCount(device);
        var sampleRate = GetSampleRate(device);

        return new AudioDeviceInfo(
            Id: device.ID,
            Name: device.FriendlyName,
            InterfaceName: device.DeviceFriendlyName ?? device.FriendlyName,
            Type: type,
            IsDefault: IsDefaultDevice(device, isInput),
            IsAvailable: device.State == DeviceState.Active,
            Channels: channels,
            SampleRate: sampleRate
        );
    }

    private DeviceType DetermineDeviceType(MMDevice device)
    {
        var name = device.FriendlyName.ToLowerInvariant();
        var interfaceName = (device.DeviceFriendlyName ?? "").ToLowerInvariant();

        if (name.Contains("bluetooth") || interfaceName.Contains("bluetooth"))
            return DeviceType.Bluetooth;
        if (name.Contains("usb") || interfaceName.Contains("usb"))
            return DeviceType.Usb;
        if (name.Contains("hdmi") || interfaceName.Contains("hdmi"))
            return DeviceType.Hdmi;
        if (name.Contains("displayport") || interfaceName.Contains("display port"))
            return DeviceType.DisplayPort;
        if (name.Contains("headset"))
            return DeviceType.Headset;
        if (name.Contains("headphone") || name.Contains("earphone"))
            return DeviceType.Headphones;
        if (name.Contains("speaker"))
            return DeviceType.Speakers;
        if (name.Contains("microphone") || name.Contains("mic "))
            return DeviceType.Microphone;

        return DeviceType.Unknown;
    }

    private int GetChannelCount(MMDevice device)
    {
        try
        {
            var format = device.AudioClient?.MixFormat;
            return format?.Channels ?? 2;
        }
        catch
        {
            return 2;
        }
    }

    private int GetSampleRate(MMDevice device)
    {
        try
        {
            var format = device.AudioClient?.MixFormat;
            return (int)(format?.SampleRate ?? 48000);
        }
        catch
        {
            return 48000;
        }
    }

    private bool IsDefaultDevice(MMDevice device, bool isInput)
    {
        try
        {
            var defaultDevice = isInput
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                : _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return defaultDevice.ID == device.ID;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _enumerator?.Dispose();
    }
}