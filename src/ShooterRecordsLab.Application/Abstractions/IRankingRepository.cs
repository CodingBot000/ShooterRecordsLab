using ShooterRecordsLab.Application.Rankings;

namespace ShooterRecordsLab.Application.Abstractions;

public interface IRankingRepository
{
    Task<IReadOnlyList<RankingRow>> GetTopAsync(int top, CancellationToken cancellationToken);
}
