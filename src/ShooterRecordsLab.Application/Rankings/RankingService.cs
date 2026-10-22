using ShooterRecordsLab.Application.Abstractions;

namespace ShooterRecordsLab.Application.Rankings;

public sealed class RankingService(IRankingRepository repository)
{
    public Task<IReadOnlyList<RankingRow>> GetTopAsync(int top, CancellationToken cancellationToken)
    {
        if (top is < 1 or > 100)
        {
            throw new RecordsException("INVALID_TOP", "top must be between 1 and 100.");
        }

        return repository.GetTopAsync(top, cancellationToken);
    }
}
