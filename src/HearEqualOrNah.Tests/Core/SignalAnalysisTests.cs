using FluentAssertions;
using Xunit;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;

namespace HearEqualOrNah.Tests.Core;

public class SignalAnalysisTests
{
    private readonly SignalAnalyzer _analyzer = new();

    [Theory]
    [InlineData(0.0, double.NegativeInfinity)]
    public void AmplitudeToDb_zero_returns_negative_infinity(double amplitude, double expected)
    {
        _analyzer.AmplitudeToDb(amplitude).Should().Be(expected);
    }

    [Fact]
    public void AmplitudeToDb_one_returns_zero()
    {
        _analyzer.AmplitudeToDb(1.0).Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void AmplitudeToDb_half_returns_minus_six()
    {
        _analyzer.AmplitudeToDb(0.5).Should().BeApproximately(-6.0206, 0.001);
    }

    [Fact]
    public void CalculateRms_sine_at_half_amplitude()
    {
        var sampleRate = 48000;
        var duration = 1.0;
        var frequency = 1000.0;
        var amplitude = 0.5f;
        var count = (int)(sampleRate * duration);
        var samples = new float[count];

        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * i / sampleRate));
        }

        var rms = _analyzer.CalculateRms(samples);
        rms.Should().BeApproximately(amplitude / Math.Sqrt(2), 0.01);
    }

    [Fact]
    public void CalculateRms_empty_returns_zero()
    {
        _analyzer.CalculateRms(Array.Empty<float>()).Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(2.0, 3.5)]
    [InlineData(-18.4, -19.1)]
    public void CalculateDifferenceDb(double left, double right)
    {
        var diff = _analyzer.CalculateDifferenceDb(left, right);
        diff.Should().BeApproximately(Math.Abs(left - right), 0.0001);
    }

    [Fact]
    public void IsClipping_detects_clipped_samples()
    {
        var samples = Enumerable.Repeat(0.99f, 100).ToArray();
        _analyzer.IsClipping(samples, threshold: 0.95, sampleRatio: 0.05).Should().BeTrue();
    }

    [Fact]
    public void IsClipping_accepts_clean_signal()
    {
        var samples = Enumerable.Range(0, 1000).Select(i => (float)(0.5 * Math.Sin(i / 10.0))).ToArray();
        _analyzer.IsClipping(samples, threshold: 0.95, sampleRatio: 0.05).Should().BeFalse();
    }

    [Fact]
    public void IsNoisy_rejects_low_snr()
    {
        _analyzer.IsNoisy(ambientDb: -30, signalDb: -35, threshold: -40).Should().BeFalse();
        _analyzer.IsNoisy(ambientDb: -30, signalDb: -80, threshold: -40).Should().BeTrue();
    }

    [Fact]
    public void IsStable_with_tight_measurements()
    {
        _analyzer.IsStable(new List<double> { -18.4, -18.3, -18.5 }, varianceThreshold: 0.5).Should().BeTrue();
    }

    [Fact]
    public void IsStable_rejects_widely_varying_measurements()
    {
        _analyzer.IsStable(new List<double> { -18.4, -10.0, -25.0 }, varianceThreshold: 0.5).Should().BeFalse();
    }

    [Fact]
    public void IsStable_requires_enough_samples()
    {
        _analyzer.IsStable(new List<double> { -18.4 }, varianceThreshold: 0.5).Should().BeFalse();
    }

    [Fact]
    public void AssessQuality_detects_clipping()
    {
        var samples = Enumerable.Repeat(0.99f, 100).ToArray();
        _analyzer.AssessQuality(samples, ambientDb: -60, signalDb: -3).Should().Be(MeasurementQuality.Clipping);
    }

    [Fact]
    public void AssessQuality_detects_noise()
    {
        var samples = Enumerable.Repeat(0.001f, 100).ToArray();
        _analyzer.AssessQuality(samples, ambientDb: -35, signalDb: -85).Should().Be(MeasurementQuality.Noisy);
    }

    [Fact]
    public void AssessQuality_good_for_clean_signal()
    {
        var samples = Enumerable.Repeat(0.3f, 100).ToArray();
        _analyzer.AssessQuality(samples, ambientDb: -60, signalDb: -10).Should().Be(MeasurementQuality.Good);
    }

    [Fact]
    public void CalculateToneAmplitude_recovers_pure_tone()
    {
        var sampleRate = 48000;
        var count = (int)(sampleRate * 1.5);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * 1000.0 * i / sampleRate));
        }

        var amplitude = _analyzer.CalculateToneAmplitude(samples, 1000.0, sampleRate);
        amplitude.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void CalculateToneAmplitude_rejects_off_frequency_tone()
    {
        var sampleRate = 48000;
        var count = (int)(sampleRate * 1.5);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * 2000.0 * i / sampleRate));
        }

        var amplitude = _analyzer.CalculateToneAmplitude(samples, 1000.0, sampleRate);
        amplitude.Should().BeLessThan(0.02);
    }

    [Fact]
    public void CalculateToneAmplitude_silence_is_zero()
    {
        var silence = new float[48000];
        _analyzer.CalculateToneAmplitude(silence, 1000.0, 48000).Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void CalculateToneAmplitude_detects_tone_buried_in_noise()
    {
        var sampleRate = 48000;
        var count = (int)(sampleRate * 1.5);
        var samples = new float[count];
        var random = new Random(42);
        for (var i = 0; i < count; i++)
        {
            var noise = (random.NextDouble() - 0.5) * 0.1;
            samples[i] = (float)(0.05 * Math.Sin(2.0 * Math.PI * 1000.0 * i / sampleRate) + noise);
        }

        var amplitude = _analyzer.CalculateToneAmplitude(samples, 1000.0, sampleRate);
        amplitude.Should().BeApproximately(0.05, 0.02);
    }

    [Fact]
    public void IsStable_accepts_tightly_grouped_measurements()
    {
        _analyzer.IsStable(new List<double> { -18.0, -18.2, -18.1, -18.15 }).Should().BeTrue();
    }

    [Fact]
    public void IsNoisy_is_false_when_signal_exceeds_ambient_by_margin()
    {
        _analyzer.IsNoisy(ambientDb: -60, signalDb: -10).Should().BeFalse();
        _analyzer.IsNoisy(ambientDb: -10, signalDb: -45).Should().BeTrue();
    }
}
