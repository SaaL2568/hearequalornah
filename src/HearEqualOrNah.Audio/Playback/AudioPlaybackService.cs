namespace HearEqualOrNah.Audio.Playback;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using HearEqualOrNah.Core.Constants;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

/// <summary>
/// Plays the test tone. The output stream is opened once per test and kept running, rendering
/// silence between tones, until <see cref="ReleaseAsync"/>. Re-opening the device for every attempt
/// lets Bluetooth earphones fall asleep between tones and swallow the start of the next one, and
/// changes the render latency from attempt to attempt.
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly ISignalGenerator _signalGenerator;
    private readonly ILogger<AudioPlaybackService> _logger;
    private IWavePlayer? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private AudioDeviceInfo? _currentDevice;
    private int _sampleRate;
    private int _channels;
    private readonly object _lock = new();
    private const int RenderLatencyMs = 100;

    public event Action<double>? PlaybackProgress;
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public AudioPlaybackService(
        ISignalGenerator signalGenerator,
        ILogger<AudioPlaybackService> logger)
    {
        _signalGenerator = signalGenerator;
        _logger = logger;
    }

    public void Initialize(AudioDeviceInfo outputDevice, int sampleRate, int channels)
    {
        lock (_lock)
        {
            Stop();

            _currentDevice = outputDevice;
            _sampleRate = sampleRate;
            _channels = channels;

            CreateOutput();
            _waveOut!.Play(); // Start streaming silence now so the device is awake for the first tone.

            _logger.LogInformation("Initialized playback for device {Device} at {SampleRate}Hz {Channels}ch",
                outputDevice.Name, sampleRate, channels);
        }
    }

    public async Task PlayTestToneAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        IWavePlayer player;
        BufferedWaveProvider provider;

        lock (_lock)
        {
            if (_currentDevice == null)
                throw new InvalidOperationException("Playback not initialized");

            // Reopen if the stream was released or stopped with an error (e.g. device reconnect).
            if (_waveOut == null || _waveProvider == null)
                CreateOutput();

            player = _waveOut ?? throw new InvalidOperationException("Failed to open the playback output.");
            provider = _waveProvider ?? throw new InvalidOperationException("Failed to create the playback buffer.");

            provider.ClearBuffer();

            var tone = _signalGenerator.GenerateTestTone(
                channel,
                _sampleRate,
                TestParameters.TestDuration,
                TestParameters.TestAmplitude,
                TestParameters.TestFrequency,
                TestParameters.FadeDuration);

            var bytes = new byte[tone.Length * sizeof(float)];
            Buffer.BlockCopy(tone, 0, bytes, 0, bytes.Length);
            provider.AddSamples(bytes, 0, bytes.Length);

            if (player.PlaybackState != PlaybackState.Playing)
                player.Play();
        }

        var totalDuration = TimeSpan.FromSeconds(TestParameters.TestDuration);
        var startTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startTime;
            var progress = Math.Min(1.0, elapsed.TotalSeconds / totalDuration.TotalSeconds);
            PlaybackProgress?.Invoke(progress);

            if (progress >= 1.0)
                break;

            await Task.Delay(50, cancellationToken);
        }

        // Wall-clock time is not the end of the tone: wait until the buffered samples have been
        // handed to the device, then allow for the render latency, so the full tone reaches the
        // earphone before the caller stops capture or playback.
        var drainDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested
               && provider.BufferedBytes > 0
               && DateTime.UtcNow < drainDeadline)
        {
            await Task.Delay(20, cancellationToken);
        }

        if (!cancellationToken.IsCancellationRequested)
            await Task.Delay(RenderLatencyMs, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            Silence();
        }
    }

    /// <summary>Stops the tone but keeps the stream open (rendering silence).</summary>
    public Task StopAsync()
    {
        Silence();
        return Task.CompletedTask;
    }

    /// <summary>Closes the output stream at the end of a test.</summary>
    public Task ReleaseAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    private void Silence()
    {
        lock (_lock)
        {
            _waveProvider?.ClearBuffer();
        }
    }

    private void CreateOutput()
    {
        var device = _currentDevice
            ?? throw new InvalidOperationException("Cannot create playback output before initialization.");

        // Never leave a half-open endpoint behind if we already hold a player.
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels);
        _waveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(5),
            DiscardOnBufferOverflow = true
        };

        var player = CreateOutputDevice(device);
        try
        {
            player.Init(_waveProvider);
        }
        catch
        {
            player.Dispose();
            _waveProvider = null;
            throw;
        }

        _waveOut = player;
        _waveOut.PlaybackStopped += OnPlaybackStopped;
    }

    /// <summary>
    /// Creates the low-level playback device. Overridable so the playback lifecycle can be
    /// exercised in tests without physical audio hardware.
    /// </summary>
    internal virtual IWavePlayer CreateOutputDevice(AudioDeviceInfo outputDevice)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(outputDevice.Id);
        return new WasapiOut(device, AudioClientShareMode.Shared, false, RenderLatencyMs);
    }

    private void Stop()
    {
        lock (_lock)
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Dispose();
                _waveOut = null;
            }
            _waveProvider = null;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Playback stopped with error");

            // Drop the dead stream so the next tone reopens the device.
            lock (_lock)
            {
                if (ReferenceEquals(sender, _waveOut) && _waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Dispose();
                    _waveOut = null;
                    _waveProvider = null;
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
