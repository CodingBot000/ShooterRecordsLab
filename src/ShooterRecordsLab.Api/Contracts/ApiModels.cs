using System.Text.Json.Serialization;
using ShooterRecordsLab.Domain.Enums;

namespace ShooterRecordsLab.Api.Contracts;

public sealed record PlayerResponse(Guid PlayerId, string DisplayName, DateTimeOffset CreatedAtUtc);
public sealed record PlayerMatchResponse(Guid MatchId, DateTimeOffset PlayedAtUtc, int Kills, int Deaths, Outcome Outcome);
public sealed record PlayerStatsResponse(Guid PlayerId, long TotalMatches, long Wins, long Losses, long Draws,
    long Kills, long Deaths, decimal? WinRate, decimal? KdRatio);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
public sealed record ParticipantRequest(
    [property: JsonRequired] Guid PlayerId,
    [property: JsonRequired] int Kills,
    [property: JsonRequired] int Deaths,
    [property: JsonRequired] Outcome Outcome);
public sealed record RegisterMatchRequest(
    [property: JsonRequired] string? ExternalMatchId,
    [property: JsonRequired] DateTimeOffset PlayedAtUtc,
    [property: JsonRequired] IReadOnlyList<ParticipantRequest?>? Participants);
public sealed record ParticipantResponse(Guid PlayerId, int Kills, int Deaths, Outcome Outcome);
public sealed record MatchResponse(Guid MatchId, string ExternalMatchId, DateTimeOffset PlayedAtUtc,
    IReadOnlyList<ParticipantResponse> Participants);
public sealed record RegisterMatchResponse(Guid MatchId, string ExternalMatchId, bool Created);
