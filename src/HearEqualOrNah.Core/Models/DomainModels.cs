namespace HearEqualOrNah.Core.Models;

using HearEqualOrNah.Core.Constants;

public enum Channel
{
    Left,
    Right
}

public enum TestState
{
    Idle,
    Preparing,
    MeasuringNoise,
    ReadyForLeft,
    TestingLeft,
    ReadyForRight,
    TestingRight,
    Completed,
    Error
}

public enum MeasurementQuality
{
    Good,
    Noisy,
    Clipping,
    Unstable
}

public record AudioDeviceInfo(
    string Id,
    string Name,
    string InterfaceName,
    DeviceType Type,
    bool IsDefault,
    bool IsAvailable,
    int Channels,
    int SampleRate
);

public enum DeviceType
{
    Unknown,
    Microphone,
    Headphones,
    Speakers,
    Headset,
    Bluetooth,
    Usb,
    Hdmi,
    DisplayPort
}

public record MicrophoneMeasurement(
    Channel Channel,
    double RmsLevel,
    double DbLevel,
    double AmbientDbLevel,
    MeasurementQuality Quality,
    DateTime Timestamp,
    TimeSpan Duration,
    int SampleCount
);

public record ChannelTestResult(
    Channel Channel,
    double DbLevel,
    MeasurementQuality Quality,
    IReadOnlyList<double> MeasurementWindowDbs,
    DateTime Timestamp
);

public record BalanceTestResult(
    ChannelTestResult LeftResult,
    ChannelTestResult RightResult,
    double DifferenceDb,
    string Classification,
    string Description,
    DateTime Timestamp,
    AudioDeviceInfo MicrophoneUsed,
    AudioDeviceInfo OutputDeviceUsed,
    bool WasCorrectionApplied
);

/// <summary>
/// Per-channel Windows endpoint volume. <see cref="LeftVolume"/>/<see cref="RightVolume"/> are the
/// audio-tapered 0..1 scalars Windows shows as percentages; <see cref="LeftDb"/>/<see cref="RightDb"/>
/// are the actual attenuation in decibels, which is what correction math must use.
/// </summary>
public record WindowsChannelBalance(
    double LeftVolume,
    double RightVolume,
    bool IsSupported,
    string? UnsupportedReason,
    double LeftDb = 0.0,
    double RightDb = 0.0,
    double MinDb = -96.0,
    double MaxDb = 0.0
)
{
    /// <summary>Relative channel offset in dB (left minus right). Positive means left is louder.</summary>
    public double OffsetDb => LeftDb - RightDb;

    /// <summary>True when a non-equal software balance is in effect.</summary>
    public bool HasOffset => IsSupported && Math.Abs(OffsetDb) > 0.05;
}

/// <summary>
/// A proposed Windows channel balance, expressed in dB. <see cref="TargetOffsetDb"/> (left minus right)
/// is the quantity derived from the measurement; the absolute levels keep the louder channel at the
/// current level so overall loudness never increases.
/// </summary>
public record ProposedCorrection(
    double LeftDb,
    double RightDb,
    double CurrentLeftDb,
    double CurrentRightDb,
    double TargetOffsetDb,
    string Explanation
);

public record CalibrationProfile(
    string Id,
    string Name,
    string MicrophoneId,
    string OutputDeviceId,
    double MicrophoneDistanceCm,
    TestParametersSnapshot TestParameters,
    DateTime CreatedAt,
    DateTime LastUsed
);

public record TestParametersSnapshot(
    double TestFrequency,
    double TestAmplitude,
    double TestDuration,
    double FadeDuration,
    double MeasurementDuration,
    int SampleRate
);

public record TestHistoryEntry(
    string Id,
    DateTime Timestamp,
    BalanceTestResult Result,
    CalibrationProfile? CalibrationUsed,
    WindowsChannelBalance? BalanceBefore,
    WindowsChannelBalance? BalanceAfter
);

public record ApplicationSettings(
    double MicrophoneGain,
    int PreferredSampleRate,
    bool AutoApplyCorrection,
    bool ShowDetailedResults,
    string? LastUsedMicrophoneId,
    string? LastUsedOutputDeviceId,
    string? LastUsedCalibrationId
);

public static class TestParametersSnapshotExtensions
{
    public static TestParametersSnapshot CreateDefault() => new(
        TestFrequency: TestParameters.TestFrequency,
        TestAmplitude: TestParameters.TestAmplitude,
        TestDuration: TestParameters.TestDuration,
        FadeDuration: TestParameters.FadeDuration,
        MeasurementDuration: TestParameters.MeasurementDuration,
        SampleRate: TestParameters.SampleRate
    );
}