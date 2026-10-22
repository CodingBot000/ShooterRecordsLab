using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Application.Rankings;
using ShooterRecordsLab.Infrastructure.Persistence;

namespace ShooterRecordsLab.Infrastructure.Repositories;

public sealed class SqlRankingRepository(ShooterRecordsDbContext db) : IRankingRepository
{
    public async Task<IReadOnlyList<RankingRow>> GetTopAsync(int top, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<RankingRow>($"EXEC dbo.GetTopPlayerRankings @Top={top}").ToListAsync(cancellationToken);
}
