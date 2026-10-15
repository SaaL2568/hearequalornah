namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;

public sealed class SignalGenerator : ISignalGenerator
{
    public float[] GenerateTestTone(
        Channel channel,
        int sampleRate,
        double duration,
        double amplitude,
        double frequency,
        double fadeDuration)
    {
        var totalSamples = (int)(sampleRate * duration);
        var fadeSamples = (int)(sampleRate * fadeDuration);
        var buffer = new float[totalSamples * 2]; // Stereo

        var omega = 2.0 * Math.PI * frequency / sampleRate;

        for (int i = 0; i < totalSamples; i++)
        {
            var sample = (float)(amplitude * Math.Sin(i * omega));

            var fadeIn = i < fadeSamples ? (double)i / fadeSamples : 1.0;
            var fadeOut = i >= totalSamples - fadeSamples
                ? (double)(totalSamples - i) / fadeSamples
                : 1.0;
            var envelope = Math.Min(fadeIn, fadeOut);

            sample = (float)(sample * envelope);

            var leftIndex = i * 2;
            var rightIndex = i * 2 + 1;

            if (channel == Channel.Left)
            {
                buffer[leftIndex] = sample;
                buffer[rightIndex] = 0f;
            }
            else
            {
                buffer[leftIndex] = 0f;
                buffer[rightIndex] = sample;
            }
        }

        return buffer;
    }

    public float[] GenerateSilence(int sampleRate, double duration, int channels)
    {
        var totalSamples = (int)(sampleRate * duration * channels);
        return new float[totalSamples];
    }
}