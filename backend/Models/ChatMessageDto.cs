namespace RealTimeBuzz.Models;

public sealed record ChatMessageDto(
    string Id,
    string RoomId,
    string SenderId,
    string RecipientId,
    string Content,
    DateTimeOffset Timestamp
);
