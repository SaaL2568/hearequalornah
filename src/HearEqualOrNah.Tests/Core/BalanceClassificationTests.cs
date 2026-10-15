using FluentAssertions;
using Xunit;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;

namespace HearEqualOrNah.Tests.Core;

public class BalanceClassificationTests
{
    private readonly BalanceClassifier _classifier = new();

    [Theory]
    [InlineData(0.0, "Excellent Balance")]
    [InlineData(0.99, "Excellent Balance")]
    [InlineData(1.0, "Excellent Balance")]
    [InlineData(1.01, "Good Balance")]
    [InlineData(2.0, "Good Balance")]
    [InlineData(2.01, "Slight Imbalance")]
    [InlineData(3.0, "Slight Imbalance")]
    [InlineData(3.01, "Significant Imbalance")]
    [InlineData(6.0, "Significant Imbalance")]
    [InlineData(6.01, "Strong Imbalance")]
    [InlineData(12.0, "Strong Imbalance")]
    public void Classify_boundary_values(double differenceDb, string expected)
    {
        _classifier.Classify(differenceDb).Should().Be(expected);
    }

    [Fact]
    public void GetDescription_is_non_empty_for_all_categories()
    {
        foreach (var db in new[] { 0.0, 1.5, 2.5, 4.0, 8.0 })
        {
            _classifier.GetDescription(db).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetPossibleCauses_is_non_empty()
    {
        _classifier.GetPossibleCauses().Should().NotBeEmpty();
    }
}
