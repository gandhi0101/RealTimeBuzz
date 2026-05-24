using System.Net.Http.Json;
using RealTimerBuzz.Models;

namespace RealTimerBuzz.Services;

public sealed class ChatApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ChatOptions _options;

    public ChatApiClient(HttpClient httpClient, ChatOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/rooms", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<Room>().AsReadOnly();
        }

        var rooms = await response.Content.ReadFromJsonAsync<List<Room>>(cancellationToken);
        return rooms?.AsReadOnly() ?? Array.Empty<Room>().AsReadOnly();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/rooms/{roomId}/messages", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ChatMessage>().AsReadOnly();
        }

        var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>(cancellationToken);
        if (messages is null)
        {
            return Array.Empty<ChatMessage>().AsReadOnly();
        }

        var mapped = messages.Select(item => new ChatMessage
        {
            Id = item.Id,
            RoomId = item.RoomId,
            SenderId = item.SenderId,
            SenderName = item.SenderId,
            Content = item.Content,
            Timestamp = item.Timestamp,
            IsMine = item.SenderId.Equals(_options.UserId, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        return mapped.AsReadOnly();
    }

    public async Task SendMessageAsync(string roomId, string content, CancellationToken cancellationToken = default)
    {
        var payload = new SendMessageRequest { Content = content };
        var response = await _httpClient.PostAsJsonAsync($"api/rooms/{roomId}/messages", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed class SendMessageRequest
    {
        public required string Content { get; init; }
    }

    private sealed class MessageDto
    {
        public required string Id { get; init; }
        public required string RoomId { get; init; }
        public required string SenderId { get; init; }
        public required string RecipientId { get; init; }
        public required string Content { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}
