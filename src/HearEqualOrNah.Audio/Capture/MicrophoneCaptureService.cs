namespace HearEqualOrNah.Audio.Capture;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using HearEqualOrNah.Core.Constants;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

public sealed class MicrophoneCaptureService : IMicrophoneCaptureService, IDisposable
{
    private readonly ISignalAnalyzer _analyzer;
    private readonly ILogger<MicrophoneCaptureService> _logger;
    private RawWasapiCapture? _capture;
    private RawWasapiCapture? _monitorCapture;
    private readonly CircularBuffer _buffer;
    private AudioDeviceInfo? _currentDevice;
    private int _sampleRate;
    private readonly object _lock = new();
    private TaskCompletionSource _recordingStopped = CompletedSource();

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    public event Action<double>? LevelChanged;
    public bool IsRecording => _capture?.CaptureState == CaptureState.Capturing;
    public bool IsMonitoring => _monitorCapture?.CaptureState == CaptureState.Capturing;

    public MicrophoneCaptureService(
        ISignalAnalyzer analyzer,
        ILogger<MicrophoneCaptureService> logger)
    {
        _analyzer = analyzer;
        _logger = logger;
        _buffer = new CircularBuffer(TestParameters.SampleRate * 8); // pre-roll + tone + drain, with margin
    }

    public void Initialize(AudioDeviceInfo inputDevice, int sampleRate)
    {
        lock (_lock)
        {
            Stop();

            _currentDevice = inputDevice;
            _sampleRate = sampleRate;
            _rawModeLogged = false;

            var device = new MMDeviceEnumerator().GetDevice(inputDevice.Id);
            _capture = new RawWasapiCapture(device)
            {
                ShareMode = AudioClientShareMode.Shared,
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)
            };

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            _logger.LogInformation("Initialized capture for device {Device} at {SampleRate}Hz",
                inputDevice.Name, sampleRate);
        }
    }

    public async Task<MicrophoneMeasurement> MeasureChannelAsync(
        Channel channel,
        IAudioPlaybackService playbackService,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_capture == null)
                throw new InvalidOperationException("Capture not initialized");

            _buffer.Clear();
        }

        await StartRecordingAsync();

        int toneStartIndex;
        try
        {
            // Silent pre-roll: lets the capture stream settle and provides an ambient reference.
            await Task.Delay(TimeSpan.FromSeconds(TestParameters.MeasurementSkipDuration), cancellationToken);

            // Anchor the analysis window to the captured sample at which the tone was started,
            // rather than to wall-clock time, so capture start-up jitter cannot shift it.
            toneStartIndex = _buffer.Count;

            // Record across the whole tone; the analysis window is chosen afterwards.
            await playbackService.PlayTestToneAsync(channel, cancellationToken);
        }
        finally
        {
            await StopRecordingAndWaitAsync();
            await playbackService.StopAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var samples = _buffer.GetAllSamples();

        var settleSamples = (int)(_sampleRate * TestParameters.ToneSettleDuration);
        var measurementLength = (int)(_sampleRate * TestParameters.MeasurementDuration);
        var windowStart = toneStartIndex + settleSamples;

        if (samples.Length < windowStart + measurementLength)
            throw new InvalidOperationException(
                "The microphone delivered too little audio during the test. Check that the microphone " +
                "is not in use by another application and try again.");

        var measurementSamples = samples.AsSpan(windowStart, measurementLength).ToArray();

        var toneAmplitude = _analyzer.CalculateToneAmplitude(measurementSamples, TestParameters.TestFrequency, _sampleRate);
        var dbLevel = _analyzer.AmplitudeToDb(toneAmplitude);

        // Narrowband noise floor measured in the same window as the tone, so the SNR compares
        // like with like (broadband RMS would be meaningless against a single-bin tone level).
        var noiseFloor = _analyzer.CalculateNarrowbandNoiseFloor(measurementSamples, TestParameters.TestFrequency, _sampleRate);
        var noiseDb = _analyzer.AmplitudeToDb(noiseFloor);

        var quality = _analyzer.AssessQuality(measurementSamples, noiseDb, dbLevel);

        LogToneTimeline(channel, samples, toneStartIndex);

        return new MicrophoneMeasurement(
            Channel: channel,
            RmsLevel: toneAmplitude,
            DbLevel: dbLevel,
            AmbientDbLevel: noiseDb,
            Quality: quality,
            Timestamp: DateTime.UtcNow,
            Duration: TimeSpan.FromSeconds(TestParameters.MeasurementDuration),
            SampleCount: measurementLength
        );
    }

    /// <summary>
    /// Logs the 1 kHz level in 100 ms steps from the moment the tone was started. Shows render
    /// latency, Bluetooth wake-up and dropouts, which a single window level cannot.
    /// </summary>
    private void LogToneTimeline(Channel channel, float[] samples, int toneStartIndex)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
            return;

        var block = _sampleRate / 10;
        var levels = new List<string>();
        for (var start = toneStartIndex; start + block <= samples.Length; start += block)
        {
            var amplitude = _analyzer.CalculateToneAmplitude(
                new ReadOnlySpan<float>(samples, start, block), TestParameters.TestFrequency, _sampleRate);
            var db = _analyzer.AmplitudeToDb(amplitude);
            levels.Add(double.IsNegativeInfinity(db) ? "-inf" : db.ToString("F0"));
        }

        _logger.LogInformation("{Channel} tone timeline (dB per 100 ms from tone start, raw={Raw}): {Levels}",
            channel, IsRawMode, string.Join(" ", levels));
    }

    public async Task<double> MeasureAmbientNoiseAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_capture == null)
                throw new InvalidOperationException("Capture not initialized");

            _buffer.Clear();
        }

        await StartRecordingAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(TestParameters.NoiseMeasurementDuration), cancellationToken);
        }
        finally
        {
            await StopRecordingAndWaitAsync();
        }

        var samples = _buffer.GetAllSamples();
        var ambientRms = _analyzer.CalculateRms(samples);
        return _analyzer.AmplitudeToDb(ambientRms);
    }

    public void StartMonitoring(AudioDeviceInfo inputDevice, int sampleRate)
    {
        StopMonitoring();

        var device = new MMDeviceEnumerator().GetDevice(inputDevice.Id);
        _monitorCapture = new RawWasapiCapture(device)
        {
            ShareMode = AudioClientShareMode.Shared,
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)
        };

        _monitorCapture.DataAvailable += OnMonitorDataAvailable;
        _monitorCapture.RecordingStopped += OnMonitorRecordingStopped;

        try
        {
            _monitorCapture.StartRecording();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start microphone monitoring");
            StopMonitoring();
        }
    }

    public void StopMonitoring()
    {
        if (_monitorCapture == null)
            return;

        try
        {
            _monitorCapture.DataAvailable -= OnMonitorDataAvailable;
            _monitorCapture.RecordingStopped -= OnMonitorRecordingStopped;
            if (_monitorCapture.CaptureState != CaptureState.Stopped)
                _monitorCapture.StopRecording();
            _monitorCapture.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop microphone monitoring");
        }
        finally
        {
            _monitorCapture = null;
        }
    }

    private void OnMonitorDataAvailable(object? sender, WaveInEventArgs e)
    {
        var floatSamples = new float[e.BytesRecorded / sizeof(float)];
        Buffer.BlockCopy(e.Buffer, 0, floatSamples, 0, e.BytesRecorded);

        var rms = _analyzer.CalculateRms(floatSamples);
        var db = _analyzer.AmplitudeToDb(rms);
        LevelChanged?.Invoke(db);
    }

    private void OnMonitorRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Microphone monitoring stopped with error");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var floatSamples = new float[e.BytesRecorded / sizeof(float)];
        Buffer.BlockCopy(e.Buffer, 0, floatSamples, 0, e.BytesRecorded);
        _buffer.Write(floatSamples);

        var rms = _analyzer.CalculateRms(floatSamples);
        var db = _analyzer.AmplitudeToDb(rms);
        LevelChanged?.Invoke(db);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error");
        }

        _recordingStopped.TrySetResult();
    }

    private async Task StartRecordingAsync()
    {
        // A previous stop that timed out may still be finishing; never start on top of it.
        await _recordingStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (_lock)
        {
            if (_capture == null)
                throw new InvalidOperationException("Capture not initialized");

            _recordingStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _capture.StartRecording();
            LogRawMode(_capture);
        }
    }

    private bool _rawModeLogged;

    private void LogRawMode(RawWasapiCapture capture)
    {
        if (_rawModeLogged)
            return;
        _rawModeLogged = true;

        if (capture.IsRawMode)
            _logger.LogInformation("Microphone opened in RAW mode (Windows capture processing bypassed)");
        else
            _logger.LogWarning("Microphone RAW mode unavailable ({Reason}); Windows capture processing may affect readings",
                capture.RawModeFailure);
    }

    /// <summary>True when the measurement stream bypasses Windows capture processing.</summary>
    public bool IsRawMode => _capture?.IsRawMode ?? false;

    /// <summary>
    /// Stops recording and waits asynchronously for the capture thread to finish. Waiting must not
    /// block the calling (UI) thread: the capture thread may be marshalling a level update to it.
    /// </summary>
    private async Task StopRecordingAndWaitAsync()
    {
        Task stopped;
        lock (_lock)
        {
            if (_capture == null || _capture.CaptureState == CaptureState.Stopped)
                return;

            stopped = _recordingStopped.Task;
            _capture.StopRecording();
        }

        try
        {
            await stopped.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out waiting for microphone capture to stop");
        }
    }

    private void Stop()
    {
        lock (_lock)
        {
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.StopRecording();
                _capture.Dispose();
                _capture = null;
            }
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        Stop();
    }
}

internal sealed class CircularBuffer
{
    private readonly float[] _buffer;
    private readonly object _sync = new();
    private int _writeIndex;
    private int _count;

    public CircularBuffer(int capacity)
    {
        _buffer = new float[capacity];
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_sync)
        {
            foreach (var sample in samples)
            {
                _buffer[_writeIndex] = sample;
                _writeIndex = (_writeIndex + 1) % _buffer.Length;
                _count = Math.Min(_count + 1, _buffer.Length);
            }
        }
    }

    public int Count
    {
        get { lock (_sync) return _count; }
    }

    public float[] GetAllSamples()
    {
        lock (_sync)
        {
            var result = new float[_count];
            if (_count == 0) return result;

            var startIndex = (_writeIndex - _count + _buffer.Length) % _buffer.Length;

            if (startIndex + _count <= _buffer.Length)
            {
                Array.Copy(_buffer, startIndex, result, 0, _count);
            }
            else
            {
                var firstPart = _buffer.Length - startIndex;
                Array.Copy(_buffer, startIndex, result, 0, firstPart);
                Array.Copy(_buffer, 0, result, firstPart, _count - firstPart);
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _writeIndex = 0;
            _count = 0;
        }
    }
}