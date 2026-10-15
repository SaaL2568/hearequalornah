namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _filePath = FilePaths.SettingsPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoryExists();
    }

    public async Task<ApplicationSettings> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new ApplicationSettings(
                MicrophoneGain: 1.0,
                PreferredSampleRate: TestParameters.SampleRate,
                AutoApplyCorrection: false,
                ShowDetailedResults: true,
                LastUsedMicrophoneId: null,
                LastUsedOutputDeviceId: null,
                LastUsedCalibrationId: null
            );
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var settings = JsonSerializer.Deserialize<ApplicationSettings>(json, _jsonOptions);
            return settings ?? CreateDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            return CreateDefault();
        }
    }

    public async Task SaveAsync(ApplicationSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation("Saved settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    private static ApplicationSettings CreateDefault() => new(
        MicrophoneGain: 1.0,
        PreferredSampleRate: TestParameters.SampleRate,
        AutoApplyCorrection: false,
        ShowDetailedResults: true,
        LastUsedMicrophoneId: null,
        LastUsedOutputDeviceId: null,
        LastUsedCalibrationId: null
    );

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}