using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealTimeBuzz.Models;

public sealed class ChatMessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; init; }

    [BsonElement("messageId")]
    public required string MessageId { get; init; }

    [BsonElement("roomId")]
    public required string RoomId { get; init; }

    [BsonElement("senderId")]
    public required string SenderId { get; init; }

    [BsonElement("recipientId")]
    public required string RecipientId { get; init; }

    [BsonElement("content")]
    public required string Content { get; init; }

    [BsonElement("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
