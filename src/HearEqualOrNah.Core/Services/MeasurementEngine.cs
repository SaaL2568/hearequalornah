namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;
using Microsoft.Extensions.Logging;

public sealed class MeasurementEngine : IMeasurementEngine
{
    private readonly IAudioPlaybackService _playbackService;
    private readonly IMicrophoneCaptureService _captureService;
    private readonly ISignalAnalyzer _analyzer;
    private readonly IBalanceClassifier _classifier;
    private readonly ILogger<MeasurementEngine> _logger;

    public event Action<TestState>? StateChanged;
    public event Action<Channel, double, MeasurementQuality>? ChannelMeasured;
    public event Action<double>? LiveLevelChanged;
    public event Action<string>? ErrorOccurred;

    public MeasurementEngine(
        IAudioPlaybackService playbackService,
        IMicrophoneCaptureService captureService,
        ISignalAnalyzer analyzer,
        IBalanceClassifier classifier,
        ILogger<MeasurementEngine> logger)
    {
        _playbackService = playbackService;
        _captureService = captureService;
        _analyzer = analyzer;
        _classifier = classifier;
        _logger = logger;

        _captureService.LevelChanged += level => LiveLevelChanged?.Invoke(level);
    }

    private AudioDeviceInfo? _microphone;
    private AudioDeviceInfo? _outputDevice;
    private ChannelTestResult? _leftResult;

    public async Task<bool> PrepareAsync(
        AudioDeviceInfo microphone,
        AudioDeviceInfo outputDevice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Preparing balance test with microphone {Mic} and output {Out}",
                microphone.Name, outputDevice.Name);

            _microphone = microphone;
            _outputDevice = outputDevice;
            _leftResult = null;

            SetState(TestState.Preparing);

            _playbackService.Initialize(outputDevice, TestParameters.SampleRate, TestParameters.Channels);
            _captureService.Initialize(microphone, TestParameters.SampleRate);

            SetState(TestState.MeasuringNoise);
            var ambientDb = await _captureService.MeasureAmbientNoiseAsync(cancellationToken);
            _logger.LogDebug("Ambient noise: {Ambient} dB", ambientDb);

            // Exact digital silence means no real microphone signal: the device is muted, disabled,
            // or Windows privacy settings are blocking desktop apps from using it.
            if (double.IsNegativeInfinity(ambientDb) || double.IsNaN(ambientDb))
            {
                ErrorOccurred?.Invoke(
                    "The microphone is delivering complete silence. Check that it is not muted and that " +
                    "Windows Settings > Privacy & security > Microphone allows desktop apps to use it.");
                SetState(TestState.Error);
                return false;
            }

            SetState(TestState.ReadyForLeft);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Test preparation cancelled");
            SetState(TestState.Error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test preparation failed");
            ErrorOccurred?.Invoke(ex.Message);
            SetState(TestState.Error);
            return false;
        }
    }

    public async Task<ChannelTestResult?> MeasureLeftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SetState(TestState.TestingLeft);
            var leftResult = await MeasureChannelAsync(Channel.Left, cancellationToken);
            if (leftResult == null)
            {
                SetState(TestState.Error);
                return null;
            }

            _leftResult = leftResult;
            ChannelMeasured?.Invoke(Channel.Left, leftResult.DbLevel, leftResult.Quality);

            SetState(TestState.ReadyForRight);
            return leftResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Left measurement cancelled");
            SetState(TestState.Error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Left measurement failed");
            ErrorOccurred?.Invoke(ex.Message);
            SetState(TestState.Error);
            return null;
        }
    }

    public async Task<BalanceTestResult?> MeasureRightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_leftResult == null || _microphone == null || _outputDevice == null)
                throw new InvalidOperationException("Left channel has not been measured yet.");

            SetState(TestState.TestingRight);
            var rightResult = await MeasureChannelAsync(Channel.Right, cancellationToken);
            if (rightResult == null)
            {
                SetState(TestState.Error);
                return null;
            }

            ChannelMeasured?.Invoke(Channel.Right, rightResult.DbLevel, rightResult.Quality);

            var leftResult = _leftResult;
            var differenceDb = _analyzer.CalculateDifferenceDb(leftResult.DbLevel, rightResult.DbLevel);
            var classification = _classifier.Classify(differenceDb);
            var description = _classifier.GetDescription(differenceDb);

            var result = new BalanceTestResult(
                LeftResult: leftResult,
                RightResult: rightResult,
                DifferenceDb: differenceDb,
                Classification: classification,
                Description: description,
                Timestamp: DateTime.UtcNow,
                MicrophoneUsed: _microphone,
                OutputDeviceUsed: _outputDevice,
                WasCorrectionApplied: false
            );

            SetState(TestState.Completed);
            _logger.LogInformation("Test completed. Difference: {Diff} dB, Classification: {Class}",
                differenceDb, classification);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Right measurement cancelled");
            SetState(TestState.Error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Right measurement failed");
            ErrorOccurred?.Invoke(ex.Message);
            SetState(TestState.Error);
            return null;
        }
    }

    private async Task<ChannelTestResult?> MeasureChannelAsync(
        Channel channel,
        CancellationToken cancellationToken)
    {
        var measurements = new List<double>();
        var attempts = 0;
        var noisyRejected = 0;

        while (measurements.Count < AnalysisThresholds.MinimumStableMeasurements &&
               attempts < AnalysisThresholds.MaximumMeasurementAttempts)
        {
            attempts++;
            cancellationToken.ThrowIfCancellationRequested();

            var measurement = await _captureService.MeasureChannelAsync(
                channel,
                _playbackService,
                cancellationToken);

            if (measurement.Quality == MeasurementQuality.Clipping)
            {
                ErrorOccurred?.Invoke("Microphone input is clipping. Lower the microphone input level and try again.");
                return null;
            }

            _logger.LogInformation(
                "{Channel} attempt {Attempt}: tone {Tone:F1} dB, noise floor {Noise:F1} dB, quality {Quality}",
                channel, attempts, measurement.DbLevel, measurement.AmbientDbLevel, measurement.Quality);

            var snr = measurement.DbLevel - measurement.AmbientDbLevel;
            if (double.IsNegativeInfinity(measurement.DbLevel) || !(snr >= AnalysisThresholds.ToneDetectionSnrDb))
            {
                ErrorOccurred?.Invoke(
                    $"The test tone was not detected on the {channel.ToString().ToUpperInvariant()} channel " +
                    $"(tone {FormatDb(measurement.DbLevel)}, only {snr:F0} dB above the background noise). " +
                    "Hold the earphone's sound outlet as close to the microphone opening as possible, " +
                    "check that the correct output device is selected, and turn the volume up if needed.");
                return null;
            }

            if (measurement.Quality == MeasurementQuality.Noisy)
            {
                noisyRejected++;
                continue;
            }

            measurements.Add(measurement.DbLevel);
        }

        if (measurements.Count < AnalysisThresholds.MinimumStableMeasurements)
        {
            var message = noisyRejected > 0
                ? "Could not capture enough clean measurements because of background noise. " +
                  "Reduce background noise, keep the earphone still, and try again."
                : "Could not complete enough stable measurements. Keep the earphone still and try again.";
            ErrorOccurred?.Invoke(message);
            return null;
        }

        var median = Median(measurements);
        var stable = measurements.Where(m => Math.Abs(m - median) <= AnalysisThresholds.StabilityVarianceThresholdDb).ToList();

        if (stable.Count < 2)
        {
            ErrorOccurred?.Invoke(
                "The measurement varied too much between attempts. Hold the earphone steady at the same " +
                "distance from the microphone and try again.");
            return null;
        }

        var finalMedian = Median(stable);
        var quality = stable.Count < measurements.Count
            ? MeasurementQuality.Unstable
            : MeasurementQuality.Good;

        return new ChannelTestResult(
            Channel: channel,
            DbLevel: finalMedian,
            Quality: quality,
            MeasurementWindowDbs: stable,
            Timestamp: DateTime.UtcNow
        );
    }

    private static string FormatDb(double db) =>
        double.IsNegativeInfinity(db) ? "silence" : $"{db:F1} dB";

    public static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private void SetState(TestState state)
    {
        // The output stream stays open (rendering silence) from preparation until the test ends,
        // including while the user repositions the earphone between LEFT and RIGHT.
        if (state is TestState.Completed or TestState.Error)
            _ = _playbackService.ReleaseAsync();

        _logger.LogDebug("State changed to {State}", state);
        StateChanged?.Invoke(state);
    }
}

public interface IMeasurementEngine
{
    event Action<TestState>? StateChanged;
    event Action<Channel, double, MeasurementQuality>? ChannelMeasured;
    event Action<double>? LiveLevelChanged;
    event Action<string>? ErrorOccurred;
    Task<bool> PrepareAsync(
        AudioDeviceInfo microphone,
        AudioDeviceInfo outputDevice,
        CancellationToken cancellationToken = default);
    Task<ChannelTestResult?> MeasureLeftAsync(CancellationToken cancellationToken = default);
    Task<BalanceTestResult?> MeasureRightAsync(CancellationToken cancellationToken = default);
}