namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Models;

/// <summary>
/// Computes the Windows channel balance that would compensate a measured acoustic imbalance.
/// The user can apply it from the app or set it themselves in Windows.
/// All math is done in decibels against the per-channel endpoint level (the Windows volume
/// scalar is audio-tapered, not linear, so it cannot be multiplied by a gain factor).
/// The correction is cumulative: it adjusts the balance that was in effect during the
/// measurement. The louder channel stays at the current level and the other channel is
/// attenuated, so applying a correction never makes the output louder.
/// </summary>
public sealed class CorrectionCalculator : ICorrectionCalculator
{
    public const double NoCorrectionThresholdDb = 0.1;

    public ProposedCorrection Calculate(
        BalanceTestResult testResult,
        WindowsChannelBalance balanceDuringTest)
    {
        // Positive acousticDelta means the left channel needs more level relative to right.
        var acousticDelta = testResult.RightResult.DbLevel - testResult.LeftResult.DbLevel;
        var differenceDb = Math.Abs(acousticDelta);

        if (differenceDb < NoCorrectionThresholdDb)
        {
            return new ProposedCorrection(
                LeftDb: balanceDuringTest.LeftDb,
                RightDb: balanceDuringTest.RightDb,
                CurrentLeftDb: balanceDuringTest.LeftDb,
                CurrentRightDb: balanceDuringTest.RightDb,
                TargetOffsetDb: balanceDuringTest.OffsetDb,
                Explanation: "Channels are already well balanced. No correction needed.");
        }

        var targetOffset = balanceDuringTest.OffsetDb + acousticDelta;
        var (left, right) = ApplyOffset(targetOffset, balanceDuringTest);

        var quieter = acousticDelta > 0 ? "Left" : "Right";
        var reduced = left < right ? "left" : "right";
        var reducedBy = Math.Abs(left - right);
        var achieved = left - right;
        var limited = Math.Abs(achieved - targetOffset) > 0.05;

        var other = reduced == "left" ? "right" : "left";
        var explanation =
            $"{quieter} channel measured {differenceDb:F1} dB quieter. " +
            (Math.Abs(targetOffset) < 0.05
                ? "Recommended: set both channels to the same level."
                : $"Recommended: keep the {other} channel where it is and lower the " +
                  $"{reduced} channel to {reducedBy:F1} dB below the {other}; overall volume is unchanged.") +
            (limited ? $" The device volume range limits the correction to {Math.Abs(achieved):F1} dB." : "");

        return new ProposedCorrection(
            LeftDb: left,
            RightDb: right,
            CurrentLeftDb: balanceDuringTest.LeftDb,
            CurrentRightDb: balanceDuringTest.RightDb,
            TargetOffsetDb: targetOffset,
            Explanation: explanation);
    }

    /// <summary>
    /// Turns a left-minus-right offset into absolute channel levels, keeping the louder channel at
    /// the current maximum channel level (the master level) and clamping to the device range.
    /// Use this at apply time against the *current* balance so a master volume change made after
    /// the measurement is respected.
    /// </summary>
    public static (double LeftDb, double RightDb) ApplyOffset(double offsetDb, WindowsChannelBalance current)
    {
        var top = Math.Clamp(Math.Max(current.LeftDb, current.RightDb), current.MinDb, current.MaxDb);
        var left = offsetDb >= 0 ? top : top + offsetDb;
        var right = offsetDb >= 0 ? top - offsetDb : top;
        return (Math.Clamp(left, current.MinDb, current.MaxDb), Math.Clamp(right, current.MinDb, current.MaxDb));
    }
}

public interface ICorrectionCalculator
{
    ProposedCorrection Calculate(BalanceTestResult testResult, WindowsChannelBalance balanceDuringTest);
}
