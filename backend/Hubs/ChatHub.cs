using Microsoft.AspNetCore.SignalR;
using RealTimeBuzz.Models;
using RealTimeBuzz.Services;

namespace RealTimeBuzz.Hubs;

public sealed class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly ChatMessageRepository _repository;

    public ChatHub(ILogger<ChatHub> logger, ChatMessageRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task JoinRoom(string room)
    {
        _logger.LogInformation("JoinRoom {Room} by {ConnectionId}", room, Context.ConnectionId);
        return Groups.AddToGroupAsync(Context.ConnectionId, room);
    }

    public Task LeaveRoom(string room)
    {
        _logger.LogInformation("LeaveRoom {Room} by {ConnectionId}", room, Context.ConnectionId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoomId) || string.IsNullOrWhiteSpace(request.SenderId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return;
        }

        var message = new ChatMessageDto(
            string.IsNullOrWhiteSpace(request.ClientMessageId) ? Guid.NewGuid().ToString("N") : request.ClientMessageId,
            request.RoomId,
            request.SenderId,
            request.RecipientId,
            request.Content.Trim(),
            DateTimeOffset.UtcNow
        );

        var document = new ChatMessageDocument
        {
            MessageId = message.Id,
            RoomId = message.RoomId,
            SenderId = message.SenderId,
            RecipientId = message.RecipientId,
            Content = message.Content,
            CreatedAt = message.Timestamp
        };

        try
        {
            await _repository.UpsertAsync(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist message {MessageId}", message.Id);
        }

        _logger.LogInformation("SendMessage {RoomId} from {SenderId} to {RecipientId}", request.RoomId, request.SenderId, request.RecipientId);

        await Clients.Group(request.RoomId)
            .SendAsync("messageReceived", message);
    }
}
