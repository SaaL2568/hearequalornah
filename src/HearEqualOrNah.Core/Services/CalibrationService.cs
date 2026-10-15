namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class CalibrationService : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _activeCalibrationId;

    public CalibrationService(ILogger<CalibrationService> logger)
    {
        _logger = logger;
        _filePath = FilePaths.CalibrationPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoryExists();
    }

    public async Task<CalibrationProfile?> GetActiveCalibrationAsync()
    {
        var profiles = await GetAllCalibrationsAsync();
        if (_activeCalibrationId != null)
        {
            return profiles.FirstOrDefault(p => p.Id == _activeCalibrationId);
        }
        return profiles.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CalibrationProfile>> GetAllCalibrationsAsync()
    {
        if (!File.Exists(_filePath))
            return Array.Empty<CalibrationProfile>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var profiles = JsonSerializer.Deserialize<List<CalibrationProfile>>(json, _jsonOptions);
            return profiles?.OrderByDescending(p => p.LastUsed).ToList() ?? new List<CalibrationProfile>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load calibrations");
            return new List<CalibrationProfile>();
        }
    }

    public async Task<CalibrationProfile> SaveCalibrationAsync(CalibrationProfile profile)
    {
        var profiles = (await GetAllCalibrationsAsync()).ToList();
        var existingIndex = profiles.FindIndex(p => p.Id == profile.Id);

        var updatedProfile = new CalibrationProfile(
            profile.Id,
            profile.Name,
            profile.MicrophoneId,
            profile.OutputDeviceId,
            profile.MicrophoneDistanceCm,
            profile.TestParameters,
            profile.CreatedAt,
            DateTime.UtcNow);

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = updatedProfile;
        }
        else
        {
            profiles.Add(updatedProfile);
        }

        await SaveAllAsync(profiles);
        _logger.LogInformation("Saved calibration {Id}", profile.Id);
        return updatedProfile;
    }

    public async Task DeleteCalibrationAsync(string id)
    {
        var profiles = (await GetAllCalibrationsAsync()).ToList();
        profiles.RemoveAll(p => p.Id == id);

        if (_activeCalibrationId == id)
        {
            _activeCalibrationId = null;
        }

        await SaveAllAsync(profiles);
        _logger.LogInformation("Deleted calibration {Id}", id);
    }

    public async Task SetActiveCalibrationAsync(string? id)
    {
        _activeCalibrationId = id;
        await SaveSettingsAsync();
        _logger.LogInformation("Set active calibration to {Id}", id ?? "none");
    }

    private async Task SaveAllAsync(List<CalibrationProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task SaveSettingsAsync()
    {
        var settingsPath = Path.Combine(Path.GetDirectoryName(_filePath)!, "calibration_settings.json");
        var settings = new { ActiveCalibrationId = _activeCalibrationId };
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(settingsPath, json);
    }
}