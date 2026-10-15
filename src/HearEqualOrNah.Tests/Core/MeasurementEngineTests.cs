using FluentAssertions;
using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HearEqualOrNah.Tests.Core;

public class MeasurementEngineTests
{
    private sealed class Harness
    {
        public Mock<IAudioPlaybackService> Playback { get; } = new();
        public Mock<IMicrophoneCaptureService> Capture { get; } = new();
        public MeasurementEngine Engine { get; }
        public List<string> Errors { get; } = new();

        public Harness()
        {
            Playback
                .Setup(p => p.PlayTestToneAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Playback
                .Setup(p => p.StopAsync())
                .Returns(Task.CompletedTask);

            Capture
                .Setup(c => c.MeasureAmbientNoiseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(-60.0);

            Engine = new MeasurementEngine(
                Playback.Object,
                Capture.Object,
                new SignalAnalyzer(),
                new BalanceClassifier(),
                NullLogger<MeasurementEngine>.Instance);

            Engine.ErrorOccurred += message => Errors.Add(message);
        }

        public void Queue(params (double Db, MeasurementQuality Quality)[] windows)
        {
            var sequence = Capture.SetupSequence(c => c.MeasureChannelAsync(
                It.IsAny<Channel>(),
                It.IsAny<IAudioPlaybackService>(),
                It.IsAny<CancellationToken>()));

            foreach (var window in windows)
                sequence = sequence.ReturnsAsync(MakeMeasurement(window.Db, window.Quality));
        }

        public Task<bool> PrepareAsync() => Engine.PrepareAsync(Device("Microphone"), Device("Output"));

        public Task<ChannelTestResult?> MeasureLeftAsync() => Engine.MeasureLeftAsync();

        public void VerifyCaptureAttempts(Times times) =>
            Capture.Verify(c => c.MeasureChannelAsync(
                It.IsAny<Channel>(),
                It.IsAny<IAudioPlaybackService>(),
                It.IsAny<CancellationToken>()), times);

        private static MicrophoneMeasurement MakeMeasurement(double db, MeasurementQuality quality) =>
            new(
                Channel.Left,
                Math.Pow(10.0, db / 20.0),
                db,
                -60.0,
                quality,
                DateTime.UtcNow,
                TimeSpan.FromSeconds(1.5),
                72000);

        private static AudioDeviceInfo Device(string name) =>
            new(name, name, name, DeviceType.Headphones, true, true, 2, 48000);
    }

    [Fact]
    public async Task MeasureLeft_retries_noisy_windows_until_it_has_enough_stable_measurements()
    {
        var harness = new Harness();
        harness.Queue(
            (-30.0, MeasurementQuality.Noisy),
            (-20.5, MeasurementQuality.Good),
            (-20.0, MeasurementQuality.Good),
            (-19.5, MeasurementQuality.Good));

        (await harness.PrepareAsync()).Should().BeTrue();

        var result = await harness.MeasureLeftAsync();

        result.Should().NotBeNull();
        result!.Quality.Should().Be(MeasurementQuality.Good);
        result.DbLevel.Should().BeApproximately(-20.0, 0.01);
        harness.VerifyCaptureAttempts(Times.Exactly(4));
        harness.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task MeasureLeft_aborts_when_input_is_clipping()
    {
        var harness = new Harness();
        harness.Queue((-3.0, MeasurementQuality.Clipping));

        (await harness.PrepareAsync()).Should().BeTrue();

        var result = await harness.MeasureLeftAsync();

        result.Should().BeNull();
        harness.VerifyCaptureAttempts(Times.Once());
        harness.Errors.Should().ContainSingle().Which.Should().Contain("clipping");
    }

    [Fact]
    public async Task MeasureLeft_aborts_when_tone_is_not_detected()
    {
        var harness = new Harness();
        harness.Queue((-70.0, MeasurementQuality.Good));

        (await harness.PrepareAsync()).Should().BeTrue();

        var result = await harness.MeasureLeftAsync();

        result.Should().BeNull();
        harness.VerifyCaptureAttempts(Times.Once());
        harness.Errors.Should().ContainSingle().Which.Should().Contain("not detected");
    }

    [Fact]
    public async Task MeasureLeft_reports_unstable_quality_when_windows_vary()
    {
        var harness = new Harness();
        harness.Queue(
            (-20.0, MeasurementQuality.Good),
            (-20.1, MeasurementQuality.Good),
            (-10.0, MeasurementQuality.Good));

        (await harness.PrepareAsync()).Should().BeTrue();

        var result = await harness.MeasureLeftAsync();

        result.Should().NotBeNull();
        result!.Quality.Should().Be(MeasurementQuality.Unstable);
    }

    [Fact]
    public async Task MeasureRight_produces_balance_result_from_left_and_right_levels()
    {
        var harness = new Harness();
        harness.Queue(
            (-20.0, MeasurementQuality.Good),
            (-20.0, MeasurementQuality.Good),
            (-20.0, MeasurementQuality.Good),
            (-14.0, MeasurementQuality.Good),
            (-14.0, MeasurementQuality.Good),
            (-14.0, MeasurementQuality.Good));

        (await harness.PrepareAsync()).Should().BeTrue();
        (await harness.MeasureLeftAsync()).Should().NotBeNull();

        var balance = await harness.Engine.MeasureRightAsync();

        balance.Should().NotBeNull();
        balance!.DifferenceDb.Should().BeApproximately(6.0, 0.01);
        balance.Classification.Should().Be("Significant Imbalance");
    }
}
