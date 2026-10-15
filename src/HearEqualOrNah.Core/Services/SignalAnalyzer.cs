namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;

public sealed class SignalAnalyzer : ISignalAnalyzer
{
    public double CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
            return 0.0;

        double sum = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Math.Sqrt(sum / samples.Length);
    }

    public double CalculateToneAmplitude(ReadOnlySpan<float> samples, double frequency, int sampleRate)
    {
        if (samples.Length == 0 || sampleRate <= 0)
            return 0.0;

        var omega = 2.0 * Math.PI * Math.Clamp(frequency, 1.0, sampleRate / 2.0 - 1.0) / sampleRate;
        var coeff = 2.0 * Math.Cos(omega);
        double s0 = 0.0, s1 = 0.0, s2 = 0.0;

        for (int i = 0; i < samples.Length; i++)
        {
            s0 = samples[i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        var real = s1 - s2 * Math.Cos(omega);
        var imag = s2 * Math.Sin(omega);
        return 2.0 * Math.Sqrt(real * real + imag * imag) / samples.Length;
    }

    /// <summary>
    /// Offsets (Hz) of the probe frequencies used to estimate the noise floor around the test tone.
    /// Chosen so none is a 50 Hz or 60 Hz mains harmonic near 1 kHz.
    /// </summary>
    private static readonly double[] NoiseProbeOffsetsHz = { -70.0, -30.0, 30.0, 70.0 };

    /// <summary>
    /// Estimates the noise amplitude in the analysis bandwidth around <paramref name="frequency"/>
    /// using Hann-windowed Goertzel probes on nearby frequencies. The result is directly comparable
    /// with <see cref="CalculateToneAmplitude"/> over the same samples, giving a true narrowband SNR.
    /// </summary>
    public double CalculateNarrowbandNoiseFloor(ReadOnlySpan<float> samples, double frequency, int sampleRate)
    {
        if (samples.Length == 0 || sampleRate <= 0)
            return 0.0;

        double sumSquares = 0.0;
        foreach (var offset in NoiseProbeOffsetsHz)
        {
            var amplitude = HannGoertzelAmplitude(samples, frequency + offset, sampleRate);
            sumSquares += amplitude * amplitude;
        }
        return Math.Sqrt(sumSquares / NoiseProbeOffsetsHz.Length);
    }

    private static double HannGoertzelAmplitude(ReadOnlySpan<float> samples, double frequency, int sampleRate)
    {
        var n = samples.Length;
        var omega = 2.0 * Math.PI * Math.Clamp(frequency, 1.0, sampleRate / 2.0 - 1.0) / sampleRate;
        var coeff = 2.0 * Math.Cos(omega);
        double s1 = 0.0, s2 = 0.0, windowSum = 0.0;

        for (int i = 0; i < n; i++)
        {
            var w = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / Math.Max(1, n - 1));
            windowSum += w;
            var s0 = samples[i] * w + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        var real = s1 - s2 * Math.Cos(omega);
        var imag = s2 * Math.Sin(omega);
        return windowSum > 0 ? 2.0 * Math.Sqrt(real * real + imag * imag) / windowSum : 0.0;
    }

    public double AmplitudeToDb(double amplitude)
    {
        if (amplitude <= 0.0)
            return double.NegativeInfinity;
        return 20.0 * Math.Log10(amplitude);
    }

    public double CalculateDifferenceDb(double leftDb, double rightDb)
    {
        return Math.Abs(leftDb - rightDb);
    }

    public MeasurementQuality AssessQuality(
        ReadOnlySpan<float> samples,
        double ambientDb,
        double signalDb)
    {
        if (IsClipping(samples))
            return MeasurementQuality.Clipping;

        if (IsNoisy(ambientDb, signalDb))
            return MeasurementQuality.Noisy;

        return MeasurementQuality.Good;
    }

    public bool IsClipping(
        ReadOnlySpan<float> samples,
        double threshold = AnalysisThresholds.ClippingThreshold,
        double sampleRatio = AnalysisThresholds.ClippingSampleRatio)
    {
        if (samples.Length == 0)
            return false;

        int clippingCount = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i]) >= threshold)
                clippingCount++;
        }

        return (double)clippingCount / samples.Length > sampleRatio;
    }

    public bool IsNoisy(
        double ambientDb,
        double signalDb,
        double threshold = AnalysisThresholds.MinimumSnrDb)
    {
        if (double.IsNegativeInfinity(signalDb) || double.IsNegativeInfinity(ambientDb))
            return true;

        var snr = signalDb - ambientDb;
        return snr < threshold;
    }

    public bool IsStable(
        IReadOnlyList<double> measurements,
        double varianceThreshold = AnalysisThresholds.StabilityVarianceThresholdDb)
    {
        if (measurements.Count < AnalysisThresholds.MinimumStableMeasurements)
            return false;

        var mean = measurements.Average();
        var variance = measurements.Select(m => (m - mean) * (m - mean)).Average();
        var stdDev = Math.Sqrt(variance);

        return stdDev <= varianceThreshold;
    }
}