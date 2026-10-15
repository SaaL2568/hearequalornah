using FluentAssertions;
using HearEqualOrNah.Audio.Playback;
using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;
using Xunit;

namespace HearEqualOrNah.Tests.Audio;

public class AudioPlaybackServiceTests
{
    private sealed class FakePlaybackService : AudioPlaybackService
    {
        public List<Mock<IWavePlayer>> Players { get; } = new();

        public FakePlaybackService(ISignalGenerator signalGenerator)
            : base(signalGenerator, NullLogger<AudioPlaybackService>.Instance)
        {
        }

        internal override IWavePlayer CreateOutputDevice(AudioDeviceInfo outputDevice)
        {
            var player = new Mock<IWavePlayer>();
            player.Setup(p => p.PlaybackState).Returns(PlaybackState.Stopped);
            Players.Add(player);
            return player.Object;
        }
    }

    private static AudioDeviceInfo OutputDevice() =>
        new("device-id", "Test Output", "Test Output", DeviceType.Headphones, true, true, 2, 48000);

    [Fact]
    public async Task Output_stream_stays_open_between_attempts()
    {
        using var service = new FakePlaybackService(new SignalGenerator());
        service.Initialize(OutputDevice(), 48000, 2);

        // An already-cancelled token makes the call return immediately without a tone-length delay.
        await service.PlayTestToneAsync(Channel.Left, new CancellationToken(canceled: true));
        await service.StopAsync();
        await service.PlayTestToneAsync(Channel.Left, new CancellationToken(canceled: true));

        // One device for the whole test: re-opening per attempt lets devices sleep between tones.
        service.Players.Should().HaveCount(1);
        service.Players[0].Verify(p => p.Dispose(), Times.Never);
    }

    [Fact]
    public async Task PlayTestTone_after_release_reopens_output_instead_of_throwing()
    {
        using var service = new FakePlaybackService(new SignalGenerator());
        service.Initialize(OutputDevice(), 48000, 2);

        await service.ReleaseAsync();

        var act = async () =>
            await service.PlayTestToneAsync(Channel.Right, new CancellationToken(canceled: true));

        await act.Should().NotThrowAsync();
        service.Players.Should().HaveCount(2);
        service.Players[0].Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public async Task PlayTestTone_before_initialization_throws()
    {
        using var service = new FakePlaybackService(new SignalGenerator());

        var act = async () => await service.PlayTestToneAsync(Channel.Left, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
