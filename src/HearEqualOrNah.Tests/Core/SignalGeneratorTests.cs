using FluentAssertions;
using HearEqualOrNah.Core;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using Xunit;

namespace HearEqualOrNah.Tests.Core;

public class SignalGeneratorTests
{
    private readonly SignalGenerator _generator = new();

    [Fact]
    public void Left_tone_has_zero_right_channel()
    {
        var buffer = _generator.GenerateTestTone(
            Channel.Left,
            sampleRate: 48000,
            duration: 0.1,
            amplitude: 0.5,
            frequency: 1000.0,
            fadeDuration: 0.01);

        for (var i = 0; i < buffer.Length; i += 2)
        {
            buffer[i + 1].Should().Be(0f);
        }
    }

    [Fact]
    public void Right_tone_has_zero_left_channel()
    {
        var buffer = _generator.GenerateTestTone(
            Channel.Right,
            sampleRate: 48000,
            duration: 0.1,
            amplitude: 0.5,
            frequency: 1000.0,
            fadeDuration: 0.01);

        for (var i = 0; i < buffer.Length; i += 2)
        {
            buffer[i].Should().Be(0f);
        }
    }

    [Fact]
    public void Tone_amplitude_never_exceeds_configured_level()
    {
        var amplitude = 0.5;
        var buffer = _generator.GenerateTestTone(
            Channel.Left,
            sampleRate: 48000,
            duration: 0.5,
            amplitude: amplitude,
            frequency: 1000.0,
            fadeDuration: 0.05);

        buffer.Max(x => Math.Abs(x)).Should().BeLessThanOrEqualTo((float)amplitude);
    }

    [Fact]
    public void Fade_starts_and_ends_at_zero()
    {
        var buffer = _generator.GenerateTestTone(
            Channel.Left,
            sampleRate: 48000,
            duration: 0.5,
            amplitude: 0.5,
            frequency: 1000.0,
            fadeDuration: 0.05);

        buffer[0].Should().BeApproximately(0f, 0.001f);
        buffer[buffer.Length - 2].Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void Silence_is_all_zeros()
    {
        var silence = _generator.GenerateSilence(48000, 0.1, 2);
        silence.Should().OnlyContain(x => x == 0f);
    }
}