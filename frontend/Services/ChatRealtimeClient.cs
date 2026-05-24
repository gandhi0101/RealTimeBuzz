using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using RealTimerBuzz.Models;

namespace RealTimerBuzz.Services;

public sealed class ChatRealtimeClient
{
    private HubConnection? _connection;
    private readonly ChatOptions _options;
    private readonly ILogger<ChatRealtimeClient> _logger;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<NotificationItem>? NotificationReceived;
    public event Action<ConnectionStatus>? ConnectionStatusChanged;
    public event Action<string>? ConnectionError;

    public ChatRealtimeClient(ChatOptions options, ILogger<ChatRealtimeClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        _logger.LogInformation("SignalR connecting to {Url}", _options.SignalRHubUrl);
        ConnectionStatusChanged?.Invoke(ConnectionStatus.Connecting);

        _connection = new HubConnectionBuilder()
            .WithUrl(_options.SignalRHubUrl, options =>
            {
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
                options.SkipNegotiation = false;
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.Reconnecting += _ =>
        {
            ConnectionStatusChanged?.Invoke(ConnectionStatus.Reconnecting);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            ConnectionStatusChanged?.Invoke(ConnectionStatus.Online);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            ConnectionStatusChanged?.Invoke(ConnectionStatus.Offline);
            if (error is not null)
            {
                ConnectionError?.Invoke($"SignalR closed: {error.GetType().Name} {error.Message}");
            }
            return Task.CompletedTask;
        };

        _connection.On<RealtimeMessage>("messageReceived", payload =>
        {
            var message = new ChatMessage
            {
                Id = payload.Id,
                RoomId = payload.RoomId,
                SenderId = payload.SenderId,
                SenderName = payload.SenderId,
                Content = payload.Content,
                Timestamp = payload.Timestamp,
                IsMine = payload.SenderId.Equals(_options.UserId, StringComparison.OrdinalIgnoreCase)
            };

            MessageReceived?.Invoke(message);
        });
        _connection.On<NotificationItem>("notificationReceived", notification => NotificationReceived?.Invoke(notification));

        try
        {
            await _connection.StartAsync(cancellationToken);
            ConnectionStatusChanged?.Invoke(ConnectionStatus.Online);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR connection failed to {Url}", _options.SignalRHubUrl);
            ConnectionStatusChanged?.Invoke(ConnectionStatus.Offline);
            throw;
        }
    }

    public async Task JoinRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.InvokeAsync("JoinRoom", roomId, cancellationToken);
    }

    public async Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.InvokeAsync("LeaveRoom", roomId, cancellationToken);
    }

    public async Task SendMessageAsync(string roomId, string recipientId, string content, string clientMessageId)
    {
        if (_connection is null)
        {
            return;
        }

        var request = new SendMessageRequest
        {
            RoomId = roomId,
            RecipientId = recipientId,
            SenderId = _options.UserId,
            Content = content,
            ClientMessageId = clientMessageId
        };

        await _connection.InvokeAsync("SendMessage", request);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _connection = null;
        ConnectionStatusChanged?.Invoke(ConnectionStatus.Offline);
    }

    private sealed class SendMessageRequest
    {
        public required string RoomId { get; init; }
        public required string RecipientId { get; init; }
        public required string SenderId { get; init; }
        public required string Content { get; init; }
        public required string ClientMessageId { get; init; }
    }

    private sealed class RealtimeMessage
    {
        public required string Id { get; init; }
        public required string RoomId { get; init; }
        public required string SenderId { get; init; }
        public required string RecipientId { get; init; }
        public required string Content { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}
