namespace HearEqualOrNah.WindowsAudio.Balance;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.WindowsAudio.EndpointVolume;
using Microsoft.Extensions.Logging;

public sealed class WindowsBalanceService : IWindowsBalanceService, IDisposable
{
    private readonly IEndpointVolumeService _endpointVolumeService;
    private readonly ILogger<WindowsBalanceService> _logger;
    private string? _currentDeviceId;

    public event Action<string, WindowsChannelBalance>? BalanceChanged;

    public WindowsBalanceService(
        IEndpointVolumeService endpointVolumeService,
        ILogger<WindowsBalanceService> logger)
    {
        _endpointVolumeService = endpointVolumeService;
        _logger = logger;
    }

    public async Task<WindowsChannelBalance> GetBalanceAsync(string deviceId)
    {
        _currentDeviceId = deviceId;

        var isSupported = await _endpointVolumeService.IsChannelVolumeSupportedAsync(deviceId);
        if (!isSupported)
        {
            return new WindowsChannelBalance(
                LeftVolume: 1.0,
                RightVolume: 1.0,
                IsSupported: false,
                UnsupportedReason: "This Windows audio device does not expose independent LEFT/RIGHT volume control."
            );
        }

        var volumes = await _endpointVolumeService.GetChannelVolumesAsync(deviceId);
        if (volumes == null)
        {
            return new WindowsChannelBalance(
                LeftVolume: 1.0,
                RightVolume: 1.0,
                IsSupported: false,
                UnsupportedReason: "Failed to read channel volumes from the audio endpoint."
            );
        }

        var balance = new WindowsChannelBalance(
            LeftVolume: volumes.LeftVolume,
            RightVolume: volumes.RightVolume,
            IsSupported: true,
            UnsupportedReason: null,
            LeftDb: volumes.LeftDb,
            RightDb: volumes.RightDb,
            MinDb: volumes.MinDb,
            MaxDb: volumes.MaxDb
        );

        BalanceChanged?.Invoke(deviceId, balance);
        return balance;
    }

    public async Task<bool> SetBalanceAsync(string deviceId, double leftDb, double rightDb)
    {
        var success = await _endpointVolumeService.SetChannelLevelsDbAsync(deviceId, (float)leftDb, (float)rightDb);

        // Re-read so listeners get the levels Windows actually applied (including the scalars).
        if (success)
            await GetBalanceAsync(deviceId);

        return success;
    }

    public async Task<bool> IsSupportedAsync(string deviceId)
    {
        return await _endpointVolumeService.IsChannelVolumeSupportedAsync(deviceId);
    }

    public void Dispose()
    {
    }
}