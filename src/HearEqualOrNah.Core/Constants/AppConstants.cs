namespace HearEqualOrNah.Core.Constants;

public static class TestParameters
{
    public const double TestFrequency = 1000.0;
    public const double TestAmplitude = 0.5;
    public const double TestDuration = 2.5;
    public const double FadeDuration = 0.05;
    /// <summary>Silent pre-roll recorded before each tone (lets capture settle).</summary>
    public const double MeasurementSkipDuration = 0.3;
    /// <summary>
    /// Time after the tone is started before the analysis window begins. Covers render latency
    /// (WASAPI buffering, Bluetooth up to ~400 ms) and the fade-in, so the window only ever sees
    /// the steady-state tone.
    /// </summary>
    public const double ToneSettleDuration = 0.5;
    /// <summary>Analysis window length. Settle + window (2.0 s) ends well before the 2.45 s fade-out.</summary>
    public const double MeasurementDuration = 1.5;
    public const double NoiseMeasurementDuration = 0.5;
    public const int SampleRate = 48000;
    public const int Channels = 2;
}

public static class AnalysisThresholds
{
    /// <summary>
    /// Minimum narrowband SNR (tone vs. noise floor near the test frequency, same window).
    /// 25 dB bounds the noise-induced level error of one window to about ±0.5 dB (worst case);
    /// the median over several windows is tighter.
    /// </summary>
    public const double MinimumSnrDb = 25.0;
    /// <summary>
    /// Below this narrowband SNR the tone is treated as absent. Relative to the noise floor rather
    /// than an absolute level, because raw-mode microphones have very different gains.
    /// </summary>
    public const double ToneDetectionSnrDb = 10.0;
    public const double ClippingThreshold = 0.95;
    public const double ClippingSampleRatio = 0.05;
    public const double StabilityVarianceThresholdDb = 1.5;
    public const int MinimumStableMeasurements = 3;
    public const int MaximumMeasurementAttempts = 6;
}

public static class BalanceClassification
{
    public const double ExcellentMaxDb = 1.0;
    public const double GoodMaxDb = 2.0;
    public const double SlightMaxDb = 3.0;
    public const double SignificantMaxDb = 6.0;

    public static string Classify(double differenceDb) => differenceDb switch
    {
        <= ExcellentMaxDb => "Excellent Balance",
        <= GoodMaxDb => "Good Balance",
        <= SlightMaxDb => "Slight Imbalance",
        <= SignificantMaxDb => "Significant Imbalance",
        _ => "Strong Imbalance"
    };

    public static string GetDescription(double differenceDb) => differenceDb switch
    {
        <= ExcellentMaxDb => "The measured acoustic levels differ by less than 1 dB under this test setup.",
        <= GoodMaxDb => "The measured channels show good balance with a small difference under this test setup.",
        <= SlightMaxDb => "The measured channels show a slight imbalance under this test setup.",
        <= SignificantMaxDb => "The measured channels differ significantly under this test setup.",
        _ => "The measured channels show a strong imbalance under this test setup."
    };
}

public static class PossibleCauses
{
    public static readonly IReadOnlyList<string> ImbalanceCauses = new[]
    {
        "Different driver output",
        "Earwax or debris on earphone",
        "Earbud positioning difference",
        "Microphone positioning variation",
        "Microphone frequency response characteristics",
        "Background noise interference",
        "Windows audio processing or enhancements",
        "Actual hardware problem"
    };
}

public static class AudioDevice
{
    public const int DefaultBufferSizeMs = 10;
    public const int MaxDeviceNameLength = 256;
}

public static class FilePaths
{
    public static string TestHistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "hearequalornah",
        "test_history.json");

    public static string CalibrationPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "hearequalornah",
        "calibration.json");

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "hearequalornah",
        "settings.json");
}