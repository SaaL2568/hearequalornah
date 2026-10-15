namespace HearEqualOrNah.WindowsAudio.EndpointVolume;

using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads and writes per-channel endpoint volume (the Windows Left/Right channel sliders). Note: NAudio's
/// <c>AudioEndpointVolumeChannel.VolumeLevel</c> is in decibels (0 dB = full volume), while
/// <c>VolumeLevelScalar</c> is the audio-tapered 0..1 value shown as a percentage by Windows.
/// Levels are always written in dB so the applied attenuation is exact.
/// </summary>
public sealed class EndpointVolumeService : IEndpointVolumeService, IDisposable
{
    private readonly ILogger<EndpointVolumeService> _logger;
    private readonly MMDeviceEnumerator _enumerator;

    public EndpointVolumeService(ILogger<EndpointVolumeService> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
    }

    public async Task<ChannelVolumeInfo?> GetChannelVolumesAsync(string deviceId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var endpointVolume = _enumerator.GetDevice(deviceId).AudioEndpointVolume;
                if (endpointVolume == null)
                {
                    _logger.LogWarning("No AudioEndpointVolume for device {DeviceId}", deviceId);
                    return null;
                }

                var channelCount = endpointVolume.Channels.Count;
                if (channelCount < 2)
                {
                    _logger.LogInformation("Device {DeviceId} has only {Count} channel(s)", deviceId, channelCount);
                    return null;
                }

                var range = endpointVolume.VolumeRange;
                return new ChannelVolumeInfo
                {
                    LeftVolume = endpointVolume.Channels[0].VolumeLevelScalar,
                    RightVolume = endpointVolume.Channels[1].VolumeLevelScalar,
                    LeftDb = endpointVolume.Channels[0].VolumeLevel,
                    RightDb = endpointVolume.Channels[1].VolumeLevel,
                    MinDb = range.MinDecibels,
                    MaxDb = range.MaxDecibels,
                    IncrementDb = range.IncrementDecibels,
                    ChannelCount = channelCount,
                    IsMuted = endpointVolume.Mute,
                    MasterVolume = endpointVolume.MasterVolumeLevelScalar
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get channel volumes for device {DeviceId}", deviceId);
                return null;
            }
        });
    }

    public async Task<bool> SetChannelLevelsDbAsync(string deviceId, float leftDb, float rightDb)
    {
        return await Task.Run(() =>
        {
            try
            {
                var endpointVolume = _enumerator.GetDevice(deviceId).AudioEndpointVolume;
                if (endpointVolume == null || endpointVolume.Channels.Count < 2)
                {
                    _logger.LogWarning("Device {DeviceId} does not support independent channel volume", deviceId);
                    return false;
                }

                var range = endpointVolume.VolumeRange;
                leftDb = Math.Clamp(leftDb, range.MinDecibels, range.MaxDecibels);
                rightDb = Math.Clamp(rightDb, range.MinDecibels, range.MaxDecibels);

                endpointVolume.Channels[0].VolumeLevel = leftDb;
                endpointVolume.Channels[1].VolumeLevel = rightDb;

                // Hardware volume controls may quantize to their step size.
                var tolerance = Math.Max(0.1f, range.IncrementDecibels) + 0.01f;
                var left = endpointVolume.Channels[0].VolumeLevel;
                var right = endpointVolume.Channels[1].VolumeLevel;
                if (Math.Abs(left - leftDb) > tolerance || Math.Abs(right - rightDb) > tolerance)
                {
                    _logger.LogWarning("Set channel levels for {DeviceId}: expected L={LeftDb} dB R={RightDb} dB but got L={ActualLeft} dB R={ActualRight} dB",
                        deviceId, leftDb, rightDb, left, right);
                    return false;
                }

                _logger.LogInformation("Set channel levels for {DeviceId}: L={Left} dB, R={Right} dB", deviceId, left, right);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set channel levels for device {DeviceId}", deviceId);
                return false;
            }
        });
    }

    public async Task<bool> IsChannelVolumeSupportedAsync(string deviceId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var endpointVolume = _enumerator.GetDevice(deviceId).AudioEndpointVolume;
                return endpointVolume != null && endpointVolume.Channels.Count >= 2;
            }
            catch
            {
                return false;
            }
        });
    }

    public void Dispose()
    {
        _enumerator?.Dispose();
    }
}

public interface IEndpointVolumeService
{
    Task<ChannelVolumeInfo?> GetChannelVolumesAsync(string deviceId);
    Task<bool> SetChannelLevelsDbAsync(string deviceId, float leftDb, float rightDb);
    Task<bool> IsChannelVolumeSupportedAsync(string deviceId);
}

public record ChannelVolumeInfo
{
    /// <summary>Audio-tapered 0..1 scalar (what Windows displays as a percentage).</summary>
    public float LeftVolume { get; init; }
    public float RightVolume { get; init; }
    /// <summary>Channel level in dB (0 dB = full scale).</summary>
    public float LeftDb { get; init; }
    public float RightDb { get; init; }
    public float MinDb { get; init; }
    public float MaxDb { get; init; }
    public float IncrementDb { get; init; }
    public int ChannelCount { get; init; }
    public bool IsMuted { get; init; }
    public float MasterVolume { get; init; }
}
