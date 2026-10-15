using FluentAssertions;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using Xunit;

namespace HearEqualOrNah.Tests.Core;

public class CorrectionCalculatorTests
{
    private readonly CorrectionCalculator _calculator = new();

    private static BalanceTestResult CreateResult(double leftDb, double rightDb) => new(
        LeftResult: new ChannelTestResult(Channel.Left, leftDb, MeasurementQuality.Good, new[] { leftDb }, DateTime.UtcNow),
        RightResult: new ChannelTestResult(Channel.Right, rightDb, MeasurementQuality.Good, new[] { rightDb }, DateTime.UtcNow),
        DifferenceDb: Math.Abs(leftDb - rightDb),
        Classification: "",
        Description: "",
        Timestamp: DateTime.UtcNow,
        MicrophoneUsed: null!,
        OutputDeviceUsed: null!,
        WasCorrectionApplied: false);

    // Real endpoint values are in dB: e.g. 12 % on the Windows slider is about -30 dB.
    private static WindowsChannelBalance Balance(double leftDb, double rightDb, double minDb = -60, double maxDb = 0) =>
        new(0.12, 0.12, true, null, leftDb, rightDb, minDb, maxDb);

    [Fact]
    public void Left_quieter_attenuates_right_and_keeps_left_at_current_level()
    {
        var correction = _calculator.Calculate(CreateResult(leftDb: -21.4, rightDb: -18.9), Balance(-30, -30));

        correction.LeftDb.Should().BeApproximately(-30.0, 1e-9);
        correction.RightDb.Should().BeApproximately(-32.5, 1e-9);
        correction.Explanation.Should().Contain("Left channel measured 2.5 dB quieter");
    }

    [Fact]
    public void Right_quieter_attenuates_left()
    {
        var correction = _calculator.Calculate(CreateResult(leftDb: -18.9, rightDb: -21.4), Balance(-30, -30));

        correction.RightDb.Should().BeApproximately(-30.0, 1e-9);
        correction.LeftDb.Should().BeApproximately(-32.5, 1e-9);
    }

    [Fact]
    public void Correction_never_raises_either_channel_above_the_current_loudest_channel()
    {
        // Regression: at full volume (0 dB) the old code wrote values that could jump to max volume.
        foreach (var (l, r) in new[] { (-40.0, -10.0), (-10.0, -40.0), (-20.0, -19.0) })
        {
            var balance = Balance(-12, -15);
            var correction = _calculator.Calculate(CreateResult(l, r), balance);

            correction.LeftDb.Should().BeLessThanOrEqualTo(-12.0 + 1e-9);
            correction.RightDb.Should().BeLessThanOrEqualTo(-12.0 + 1e-9);
        }
    }

    [Fact]
    public void Correction_is_cumulative_with_an_existing_balance()
    {
        // A previous correction left right 3 dB down; the verification test still shows left 1 dB quieter.
        var correction = _calculator.Calculate(CreateResult(leftDb: -20, rightDb: -19), Balance(-25, -28));

        correction.TargetOffsetDb.Should().BeApproximately(4.0, 1e-9);
        correction.LeftDb.Should().BeApproximately(-25.0, 1e-9);
        correction.RightDb.Should().BeApproximately(-29.0, 1e-9);
    }

    [Fact]
    public void Applied_offset_matches_the_measured_difference_exactly()
    {
        var balance = Balance(-30, -30);
        var correction = _calculator.Calculate(CreateResult(leftDb: -23.7, rightDb: -20.0), balance);

        (correction.LeftDb - correction.RightDb).Should().BeApproximately(3.7, 1e-9);
    }

    [Fact]
    public void Correction_is_clamped_to_device_range_and_says_so()
    {
        var correction = _calculator.Calculate(CreateResult(leftDb: -50, rightDb: -10), Balance(-30, -30, minDb: -60));

        correction.RightDb.Should().BeApproximately(-60.0, 1e-9);
        correction.LeftDb.Should().BeApproximately(-30.0, 1e-9);
        correction.Explanation.Should().Contain("limits the correction");
    }

    [Fact]
    public void Already_balanced_proposes_no_change()
    {
        var correction = _calculator.Calculate(CreateResult(leftDb: -18.0, rightDb: -18.05), Balance(-20, -22));

        correction.LeftDb.Should().Be(-20);
        correction.RightDb.Should().Be(-22);
        correction.Explanation.Should().Contain("already well balanced");
    }

    [Fact]
    public void Correction_that_cancels_an_existing_offset_equalises_channels()
    {
        // Right was 2 dB down, but the measurement now shows right 2 dB quieter: undo the offset.
        var correction = _calculator.Calculate(CreateResult(leftDb: -18, rightDb: -20), Balance(-20, -22));

        correction.LeftDb.Should().BeApproximately(-20.0, 1e-9);
        correction.RightDb.Should().BeApproximately(-20.0, 1e-9);
        correction.Explanation.Should().Contain("same level");
    }

    [Fact]
    public void ApplyOffset_uses_the_current_master_level()
    {
        // The user lowered the volume after the test; the offset must apply at the new level.
        var (left, right) = CorrectionCalculator.ApplyOffset(-2.5, Balance(-40, -40));

        left.Should().BeApproximately(-42.5, 1e-9);
        right.Should().BeApproximately(-40.0, 1e-9);
    }

    [Fact]
    public void Balance_HasOffset_ignores_float_noise()
    {
        Balance(-30.0, -30.01).HasOffset.Should().BeFalse();
        Balance(-30.0, -31.0).HasOffset.Should().BeTrue();
    }
}
