namespace RealTimerBuzz.Models;

public enum PresenceStatus
{
    Offline,
    Away,
    Online
}

public sealed record class Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int MemberCount { get; init; }
    public string[] Participants { get; init; } = Array.Empty<string>();
    public bool IsMuted { get; init; }
    public bool IsGroup { get; init; }
    public string? PeerId { get; init; }
    public string? LastMessagePreview { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public PresenceStatus Status { get; init; }
}
