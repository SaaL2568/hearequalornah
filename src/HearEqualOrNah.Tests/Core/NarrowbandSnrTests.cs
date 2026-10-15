using FluentAssertions;
using HearEqualOrNah.Core.Constants;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using Xunit;

namespace HearEqualOrNah.Tests.Core;

public class NarrowbandSnrTests
{
    private const int SampleRate = 48000;
    private static readonly int WindowLength = (int)(SampleRate * TestParameters.MeasurementDuration);
    private readonly SignalAnalyzer _analyzer = new();

    private static float[] Tone(double amplitude, double noiseRms, int seed = 1, double humAmplitude = 0)
    {
        var random = new Random(seed);
        var samples = new float[WindowLength];
        for (int i = 0; i < samples.Length; i++)
        {
            // Box-Muller gaussian noise.
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            var noise = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2) * noiseRms;
            var hum = 0.0;
            for (int h = 1; h <= 25; h++)
                hum += humAmplitude * Math.Sin(2.0 * Math.PI * 50.0 * h * i / SampleRate + h);
            samples[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * 1000.0 * i / SampleRate) + noise + hum);
        }
        return samples;
    }

    [Fact]
    public void Noise_floor_tracks_white_noise_in_the_analysis_bandwidth()
    {
        // Narrowband noise for N samples of white noise with RMS sigma is about sigma * sqrt(2 / N)
        // per probe (rectangular). Hann windowing raises it by sqrt(1.5) (its noise bandwidth).
        var sigma = 0.01;
        var samples = Tone(amplitude: 0.0, noiseRms: sigma);

        var floor = _analyzer.CalculateNarrowbandNoiseFloor(samples, 1000.0, SampleRate);
        var expected = sigma * Math.Sqrt(2.0 / WindowLength) * Math.Sqrt(1.5);

        floor.Should().BeInRange(expected * 0.4, expected * 2.5);
    }

    [Fact]
    public void Strong_tone_does_not_leak_into_noise_probes()
    {
        var samples = Tone(amplitude: 0.5, noiseRms: 0.0);

        var tone = _analyzer.AmplitudeToDb(_analyzer.CalculateToneAmplitude(samples, 1000.0, SampleRate));
        var floor = _analyzer.AmplitudeToDb(_analyzer.CalculateNarrowbandNoiseFloor(samples, 1000.0, SampleRate));

        (tone - floor).Should().BeGreaterThan(80);
    }

    [Fact]
    public void Mains_hum_harmonics_do_not_count_as_noise()
    {
        var samples = Tone(amplitude: 0.05, noiseRms: 0.0, humAmplitude: 0.01);

        var tone = _analyzer.AmplitudeToDb(_analyzer.CalculateToneAmplitude(samples, 1000.0, SampleRate));
        var floor = _analyzer.AmplitudeToDb(_analyzer.CalculateNarrowbandNoiseFloor(samples, 1000.0, SampleRate));

        (tone - floor).Should().BeGreaterThan(AnalysisThresholds.MinimumSnrDb);
    }

    [Fact]
    public void Tone_buried_in_broadband_noise_is_flagged_as_noisy()
    {
        // Tone 0.0005 (-66 dBFS) under white noise RMS 0.1: narrowband SNR is roughly 15 dB.
        var samples = Tone(amplitude: 0.0005, noiseRms: 0.1);

        var toneDb = _analyzer.AmplitudeToDb(_analyzer.CalculateToneAmplitude(samples, 1000.0, SampleRate));
        var floorDb = _analyzer.AmplitudeToDb(_analyzer.CalculateNarrowbandNoiseFloor(samples, 1000.0, SampleRate));

        _analyzer.AssessQuality(samples, floorDb, toneDb).Should().Be(MeasurementQuality.Noisy);
    }

    [Fact]
    public void Clean_tone_in_quiet_room_is_good_and_accurate()
    {
        var samples = Tone(amplitude: 0.05, noiseRms: 0.001);

        var toneDb = _analyzer.AmplitudeToDb(_analyzer.CalculateToneAmplitude(samples, 1000.0, SampleRate));
        var floorDb = _analyzer.AmplitudeToDb(_analyzer.CalculateNarrowbandNoiseFloor(samples, 1000.0, SampleRate));

        _analyzer.AssessQuality(samples, floorDb, toneDb).Should().Be(MeasurementQuality.Good);
        toneDb.Should().BeApproximately(20 * Math.Log10(0.05), 0.1);
    }

    [Theory]
    [InlineData(new[] { 1.0, 2.0, 3.0 }, 2.0)]
    [InlineData(new[] { -20.0, -19.0 }, -19.5)]
    [InlineData(new[] { 4.0, 1.0, 3.0, 2.0 }, 2.5)]
    public void Median_averages_the_middle_pair_for_even_counts(double[] values, double expected)
    {
        MeasurementEngine.Median(values).Should().BeApproximately(expected, 1e-12);
    }
}
