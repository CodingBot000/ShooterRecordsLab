using ShooterRecordsLab.Domain.Enums;

namespace ShooterRecordsLab.Application.Matches;

public sealed record ParticipantInput(Guid PlayerId, int Kills, int Deaths, Outcome Outcome);
public sealed record RegisterMatchInput(string? ExternalMatchId, DateTimeOffset PlayedAtUtc, IReadOnlyList<ParticipantInput>? Participants);
public sealed record MatchView(Guid MatchId, string ExternalMatchId, DateTimeOffset PlayedAtUtc, IReadOnlyList<ParticipantInput> Participants);
public sealed record RegistrationResult(Guid MatchId, string ExternalMatchId, bool Created);
