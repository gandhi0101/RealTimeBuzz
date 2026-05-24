namespace RealTimerBuzz.Models;

public enum NotificationKind
{
    Message,
    Mention,
    System
}

public sealed class NotificationItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool IsRead { get; set; }
    public string? RoomId { get; init; }
    public NotificationKind Kind { get; init; }
}
