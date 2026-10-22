using ShooterRecordsLab.Domain.Rules;

namespace ShooterRecordsLab.UnitTests;

public sealed class StatisticsTests
{
    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(8, 0, null)]
    [InlineData(0, 5, "0")]
    [InlineData(1, 3, "0.3333")]
    [InlineData(1, 32, "0.0313")]
    [InlineData(15, 10, "1.5")]
    public void Ratio_handles_empty_and_rounds_away_from_zero(long numerator, long denominator, string? expected)
    {
        Assert.Equal(expected is null ? (decimal?)null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Statistics.Ratio(numerator, denominator));
    }
}
