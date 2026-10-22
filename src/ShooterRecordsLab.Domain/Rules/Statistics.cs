namespace ShooterRecordsLab.Domain.Rules;

public static class Statistics
{
    public static decimal? Ratio(long numerator, long denominator) => denominator == 0
        ? null
        : decimal.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);
}
