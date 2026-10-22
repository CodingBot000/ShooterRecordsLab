namespace ShooterRecordsLab.Domain.Entities;

public sealed class Player
{
    private Player() { }

    public Player(Guid id, string displayName, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 64 || createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Player requires an ID, a nickname of 1..64 characters and UTC creation time.");
        }

        Id = id;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
