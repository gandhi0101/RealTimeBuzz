namespace RealTimerBuzz.Models;

public sealed record class ChatMessage
{
    public required string Id { get; init; }
    public required string RoomId { get; init; }
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool IsMine { get; init; }
}
