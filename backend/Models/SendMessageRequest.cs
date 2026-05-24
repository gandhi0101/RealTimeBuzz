namespace RealTimeBuzz.Models;

public sealed record SendMessageRequest(
    string RoomId,
    string RecipientId,
    string SenderId,
    string Content,
    string? ClientMessageId
);
