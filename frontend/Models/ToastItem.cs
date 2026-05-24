namespace RealTimerBuzz.Models;

public sealed class ToastItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? RoomId { get; init; }
    public NotificationKind Kind { get; init; }
}
