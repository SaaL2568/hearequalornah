namespace HearEqualOrNah.Core.Interfaces;

using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDeviceInfo> GetInputDevices();
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    event Action? DevicesChanged;
    AudioDeviceInfo? GetDefaultInputDevice();
    AudioDeviceInfo? GetDefaultOutputDevice();
    bool IsDeviceAvailable(string deviceId, bool isInput);
}

public interface IAudioPlaybackService : IDisposable
{
    void Initialize(AudioDeviceInfo outputDevice, int sampleRate, int channels);
    Task PlayTestToneAsync(Channel channel, CancellationToken cancellationToken = default);
    /// <summary>Stops the current tone; the output stream stays open, rendering silence.</summary>
    Task StopAsync();
    /// <summary>Closes the output stream at the end of a test.</summary>
    Task ReleaseAsync();
    bool IsPlaying { get; }
    event Action<double>? PlaybackProgress;
}

public interface IMicrophoneCaptureService : IDisposable
{
    void Initialize(AudioDeviceInfo inputDevice, int sampleRate);
    Task<MicrophoneMeasurement> MeasureChannelAsync(
        Channel channel,
        IAudioPlaybackService playbackService,
        CancellationToken cancellationToken = default);
    Task<double> MeasureAmbientNoiseAsync(CancellationToken cancellationToken = default);
    void StartMonitoring(AudioDeviceInfo inputDevice, int sampleRate);
    void StopMonitoring();
    event Action<double>? LevelChanged;
    bool IsRecording { get; }
    bool IsMonitoring { get; }
}

public interface ISignalGenerator
{
    float[] GenerateTestTone(Channel channel, int sampleRate, double duration, double amplitude, double frequency, double fadeDuration);
    float[] GenerateSilence(int sampleRate, double duration, int channels);
}

public interface ISignalAnalyzer
{
    double CalculateRms(ReadOnlySpan<float> samples);
    double CalculateToneAmplitude(ReadOnlySpan<float> samples, double frequency, int sampleRate);
    double CalculateNarrowbandNoiseFloor(ReadOnlySpan<float> samples, double frequency, int sampleRate);
    double AmplitudeToDb(double amplitude);
    double CalculateDifferenceDb(double leftDb, double rightDb);
    MeasurementQuality AssessQuality(ReadOnlySpan<float> samples, double ambientDb, double signalDb);
    bool IsClipping(ReadOnlySpan<float> samples, double threshold = AnalysisThresholds.ClippingThreshold, double sampleRatio = AnalysisThresholds.ClippingSampleRatio);
    bool IsNoisy(double ambientDb, double signalDb, double threshold = AnalysisThresholds.MinimumSnrDb);
    bool IsStable(IReadOnlyList<double> measurements, double varianceThreshold = AnalysisThresholds.StabilityVarianceThresholdDb);
}

public interface IBalanceClassifier
{
    string Classify(double differenceDb);
    string GetDescription(double differenceDb);
    IReadOnlyList<string> GetPossibleCauses();
}

public interface IWindowsBalanceService
{
    Task<WindowsChannelBalance> GetBalanceAsync(string deviceId);
    /// <summary>Sets the per-channel endpoint levels in dB (0 dB = full scale).</summary>
    Task<bool> SetBalanceAsync(string deviceId, double leftDb, double rightDb);
    Task<bool> IsSupportedAsync(string deviceId);
    event Action<string, WindowsChannelBalance>? BalanceChanged;
}

public interface ICalibrationService
{
    Task<CalibrationProfile?> GetActiveCalibrationAsync();
    Task<IReadOnlyList<CalibrationProfile>> GetAllCalibrationsAsync();
    Task<CalibrationProfile> SaveCalibrationAsync(CalibrationProfile profile);
    Task DeleteCalibrationAsync(string id);
    Task SetActiveCalibrationAsync(string? id);
}

public interface ITestHistoryService
{
    Task<IReadOnlyList<TestHistoryEntry>> GetHistoryAsync(int maxEntries = 50);
    Task SaveTestAsync(TestHistoryEntry entry);
    Task ClearHistoryAsync();
}

public interface ISettingsService
{
    Task<ApplicationSettings> LoadAsync();
    Task SaveAsync(ApplicationSettings settings);
}