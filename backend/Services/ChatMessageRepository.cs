using MongoDB.Driver;
using RealTimeBuzz.Models;

namespace RealTimeBuzz.Services;

public sealed class ChatMessageRepository
{
    private readonly IMongoCollection<ChatMessageDocument> _messages;

    public ChatMessageRepository(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessageDocument>("messages");

        var roomIndex = Builders<ChatMessageDocument>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.CreatedAt);

        var messageIndex = Builders<ChatMessageDocument>.IndexKeys
            .Ascending(x => x.MessageId);

        _messages.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<ChatMessageDocument>(roomIndex),
            new CreateIndexModel<ChatMessageDocument>(messageIndex, new CreateIndexOptions { Unique = true })
        });
    }

    public async Task<ChatMessageDocument> UpsertAsync(ChatMessageDocument message, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ChatMessageDocument>.Filter.Eq(x => x.MessageId, message.MessageId);
        var options = new ReplaceOptions { IsUpsert = true };

        await _messages.ReplaceOneAsync(filter, message, options, cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<ChatMessageDocument>> GetByRoomAsync(
        string roomId,
        int limit = 50,
        DateTimeOffset? before = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ChatMessageDocument>.Filter.Eq(x => x.RoomId, roomId);
        if (before.HasValue)
        {
            filter &= Builders<ChatMessageDocument>.Filter.Lt(x => x.CreatedAt, before.Value);
        }

        return await _messages.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }
}
