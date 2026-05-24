using RealTimerBuzz.Models;

namespace RealTimerBuzz.Services;

public sealed class ChatStateService : IAsyncDisposable
{
    private const string PreferencesKey = "rtb.preferences";
    private const string MessagesKeyPrefix = "rtb.messages.";
    private const int MaxCachedMessages = 200;
    private readonly ChatApiClient _apiClient;
    private readonly ChatRealtimeClient _realtimeClient;
    private readonly LocalStorageService _localStorage;
    private readonly BrowserNotificationService _browserNotifications;
    private readonly ChatOptions _options;
    private readonly Random _random = new();
    private readonly Dictionary<string, List<ChatMessage>> _messagesByRoom = new();
    private readonly Dictionary<string, int> _unreadCounts = new();
    private readonly List<NotificationItem> _notifications = new();
    private readonly List<ToastItem> _toasts = new();
    private readonly HashSet<string> _joinedRooms = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _mockLoopCts;
    private bool _initialized;

    public ChatStateService(
        ChatApiClient apiClient,
        ChatRealtimeClient realtimeClient,
        LocalStorageService localStorage,
        BrowserNotificationService browserNotifications,
        ChatOptions options)
    {
        _apiClient = apiClient;
        _realtimeClient = realtimeClient;
        _localStorage = localStorage;
        _browserNotifications = browserNotifications;
        _options = options;

        _realtimeClient.MessageReceived += HandleRealtimeMessage;
        _realtimeClient.NotificationReceived += HandleRealtimeNotification;
        _realtimeClient.ConnectionError += message =>
        {
            ErrorMessage = message;
            NotifyStateChanged();
        };
        _realtimeClient.ConnectionStatusChanged += status =>
        {
            ConnectionStatus = status;
            if (status == ConnectionStatus.Online)
            {
                _ = JoinRoomsAsync(Rooms.Select(room => room.Id));
            }
            NotifyStateChanged();
        };
    }

    public IReadOnlyList<Room> Rooms { get; private set; } = Array.Empty<Room>();
    public string? SelectedRoomId { get; private set; }
    public ConnectionStatus ConnectionStatus { get; private set; } = ConnectionStatus.Offline;
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public UserPreferences Preferences { get; private set; } = new();
    public bool IsTyping { get; private set; }
    public string? TypingRoomId { get; private set; }

    public event Action? OnChange;

    public bool IsMockMode => _options.Mode == ChatMode.Mock;

    public Room? SelectedRoom => Rooms.FirstOrDefault(room => room.Id == SelectedRoomId);

    public IReadOnlyList<ChatMessage> CurrentMessages =>
        SelectedRoomId is not null && _messagesByRoom.TryGetValue(SelectedRoomId, out var messages)
            ? messages
            : Array.Empty<ChatMessage>();

    public IReadOnlyList<NotificationItem> Notifications => _notifications;

    public IReadOnlyList<ToastItem> Toasts => _toasts;

    public IReadOnlyDictionary<string, int> UnreadCounts => _unreadCounts;

    public int GlobalUnreadCount => _unreadCounts.Values.Sum();

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsLoading = true;
        NotifyStateChanged();

        try
        {
            var storedPreferences = await _localStorage.GetItemAsync<UserPreferences>(PreferencesKey);
            if (storedPreferences is not null)
            {
                Preferences = storedPreferences;
            }
        }
        catch
        {
            // Ignore local storage failures.
        }

        if (IsMockMode)
        {
            await LoadMockDataAsync();
            ConnectionStatus = ConnectionStatus.Online;
            StartMockLoop();
        }
        else
        {
            try
            {
                await _realtimeClient.ConnectAsync();
                await LoadLiveDataAsync();
                await JoinRoomsAsync(Rooms.Select(room => room.Id));
            }
            catch (Exception ex)
            {
                var detail = string.IsNullOrWhiteSpace(ex.Message)
                    ? ex.InnerException?.Message
                    : ex.Message;
                ErrorMessage = $"SignalR connection failed: {ex.GetType().Name} {detail}".Trim();
                ConnectionStatus = ConnectionStatus.Offline;
                NotifyStateChanged();
            }
        }

        await LoadCachedMessagesAsync();

        _ = _browserNotifications.RequestPermissionAsync();

        if (SelectedRoomId is null)
        {
            if (Rooms.Count > 0)
            {
                await SelectRoomAsync(Rooms[0].Id);
            }
            else if (!string.IsNullOrWhiteSpace(_options.PeerId))
            {
                await AddDirectRoomAsync(_options.PeerId, _options.PeerName);
            }
        }

        IsLoading = false;
        NotifyStateChanged();
    }

    public async Task SelectRoomAsync(string roomId)
    {
        if (SelectedRoomId == roomId)
        {
            return;
        }

        var previousRoomId = SelectedRoomId;
        SelectedRoomId = roomId;
        _unreadCounts[roomId] = 0;
        MarkRoomNotificationsRead(roomId);

        if (!IsMockMode)
        {
            await JoinRoomsAsync(new[] { roomId });

            if (!_messagesByRoom.ContainsKey(roomId))
            {
                var messages = await _apiClient.GetMessagesAsync(roomId);
                _messagesByRoom[roomId] = messages.ToList();
            }
        }

        await LoadCachedMessagesForRoomAsync(roomId);

        NotifyStateChanged();
    }

    public async Task AddDirectRoomAsync(string peerId, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return;
        }

        var normalizedPeer = peerId.Trim();
        var roomId = BuildDirectRoomId(_options.UserId, normalizedPeer);
        var name = string.IsNullOrWhiteSpace(displayName)
            ? normalizedPeer.Split('@')[0]
            : displayName.Trim();

        var existing = Rooms.FirstOrDefault(room =>
            string.Equals(room.PeerId, normalizedPeer, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var list = Rooms.ToList();
            list.Insert(0, new Room
            {
                Id = roomId,
                Name = name,
                Description = "Direct message",
                MemberCount = 2,
                Participants = new[] { name },
                IsMuted = false,
                IsGroup = false,
                PeerId = normalizedPeer,
                LastMessagePreview = "Start a conversation",
                LastMessageAt = null,
                Status = PresenceStatus.Online
            });
            Rooms = list;
        }

        await JoinRoomsAsync(new[] { roomId });
        await SelectRoomAsync(roomId);
    }

    public async Task SendMessageAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || SelectedRoomId is null)
        {
            return;
        }

        var messageId = Guid.NewGuid().ToString("N");
        var roomId = SelectedRoomId;
        if (SelectedRoom is { IsGroup: false } && !string.IsNullOrWhiteSpace(SelectedRoom.PeerId))
        {
            roomId = BuildDirectRoomId(_options.UserId, SelectedRoom.PeerId);
        }

        var message = new ChatMessage
        {
            Id = messageId,
            RoomId = roomId ?? SelectedRoomId,
            SenderId = _options.UserId,
            SenderName = "You",
            Content = content.Trim(),
            Timestamp = DateTimeOffset.Now,
            IsMine = true
        };

        AddMessage(message, raiseToast: false);

        if (!IsMockMode)
        {
            try
            {
                var recipientId = SelectedRoom?.PeerId ?? SelectedRoomId;
                await _realtimeClient.SendMessageAsync(roomId ?? SelectedRoomId, recipientId ?? SelectedRoomId, message.Content, messageId);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        NotifyStateChanged();
    }

    public void MarkAllNotificationsRead()
    {
        foreach (var notification in _notifications)
        {
            notification.IsRead = true;
        }

        NotifyStateChanged();
    }

    public void MarkRoomNotificationsRead(string roomId)
    {
        foreach (var notification in _notifications.Where(n => n.RoomId == roomId))
        {
            notification.IsRead = true;
        }

        NotifyStateChanged();
    }

    public async Task SetPreferencesAsync(UserPreferences preferences)
    {
        Preferences = preferences;
        await _localStorage.SetItemAsync(PreferencesKey, preferences);
        NotifyStateChanged();
    }

    public void DismissToast(string toastId)
    {
        var toast = _toasts.FirstOrDefault(item => item.Id == toastId);
        if (toast is null)
        {
            return;
        }

        _toasts.Remove(toast);
        NotifyStateChanged();
    }

    private void AddToast(NotificationItem notification)
    {
        var toast = new ToastItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = notification.Title,
            Body = notification.Body,
            Timestamp = notification.Timestamp,
            RoomId = notification.RoomId,
            Kind = notification.Kind
        };

        _toasts.Insert(0, toast);
        if (_toasts.Count > 4)
        {
            _toasts.RemoveAt(_toasts.Count - 1);
        }
    }

    private void AddNotification(NotificationItem notification, bool addToast)
    {
        _notifications.Insert(0, notification);
        if (addToast)
        {
            AddToast(notification);
        }
    }

    private void AddMessage(ChatMessage message, bool raiseToast)
    {
        if (!_messagesByRoom.TryGetValue(message.RoomId, out var messages))
        {
            messages = new List<ChatMessage>();
            _messagesByRoom[message.RoomId] = messages;
        }

        if (messages.Any(existing => existing.Id == message.Id))
        {
            return;
        }

        messages.Add(message);
        UpdateRoomPreview(message);
        _ = CacheMessagesAsync(message.RoomId);

        if (message.RoomId != SelectedRoomId)
        {
            _unreadCounts[message.RoomId] = _unreadCounts.TryGetValue(message.RoomId, out var count)
                ? count + 1
                : 1;
        }

        if (raiseToast)
        {
            var notification = new NotificationItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = message.IsMine ? "Sent" : message.SenderName,
                Body = message.Content,
                Timestamp = message.Timestamp,
                RoomId = message.RoomId,
                Kind = NotificationKind.Message,
                IsRead = message.RoomId == SelectedRoomId
            };

            AddNotification(notification, addToast: message.RoomId != SelectedRoomId);
            _ = NotifyBrowserAsync(notification, message.RoomId == SelectedRoomId);
        }
    }

    private async Task NotifyBrowserAsync(NotificationItem notification, bool isActiveRoom)
    {
        if (notification.Kind != NotificationKind.Message)
        {
            return;
        }

        var isFocused = await _browserNotifications.IsFocusedAsync();
        if (isActiveRoom && isFocused)
        {
            return;
        }

        await _browserNotifications.NotifyAsync(notification.Title, notification.Body);
    }

    private void UpdateRoomPreview(ChatMessage message)
    {
        if (Rooms.Count == 0)
        {
            return;
        }

        Rooms = Rooms.Select(room =>
            room.Id == message.RoomId
                ? room with
                {
                    LastMessagePreview = message.Content,
                    LastMessageAt = message.Timestamp
                }
                : room).ToList();
    }

    private async Task LoadLiveDataAsync()
    {
        try
        {
            Rooms = await _apiClient.GetRoomsAsync();
            Rooms = NormalizeRooms(Rooms);
            Rooms = EnsurePeerRoom(Rooms);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Rooms = NormalizeRooms(Array.Empty<Room>());
            Rooms = EnsurePeerRoom(Rooms);
        }
    }

    private async Task LoadMockDataAsync()
    {
        await Task.Delay(250);

        Rooms = NormalizeRooms(MockDataFactory.BuildRooms());
        foreach (var room in Rooms)
        {
            _messagesByRoom[room.Id] = MockDataFactory.BuildMessages(room.Id, room.Name);
            _unreadCounts[room.Id] = 0;
        }

        _notifications.AddRange(MockDataFactory.BuildNotifications(Rooms));
        SelectedRoomId = Rooms.FirstOrDefault()?.Id;
    }

    private void StartMockLoop()
    {
        _mockLoopCts?.Cancel();
        _mockLoopCts = new CancellationTokenSource();
        var token = _mockLoopCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var delay = _random.Next(6000, 12000);
                await Task.Delay(delay, token);

                var room = Rooms[_random.Next(Rooms.Count)];
                IsTyping = true;
                TypingRoomId = room.Id;
                NotifyStateChanged();

                await Task.Delay(_random.Next(800, 1400), token);

                var message = MockDataFactory.BuildIncomingMessage(room.Id, room.Name);
                IsTyping = false;
                TypingRoomId = null;
                AddMessage(message, raiseToast: true);
                NotifyStateChanged();
            }
        }, token);
    }

    private void HandleRealtimeMessage(ChatMessage message)
    {
        var room = Rooms.FirstOrDefault(room => room.Id == message.RoomId);
        if (room is null)
        {
            var display = message.SenderId.Split('@')[0];
            var list = Rooms.ToList();
            list.Insert(0, new Room
            {
                Id = message.RoomId,
                Name = display,
                Description = "Direct message",
                MemberCount = 2,
                Participants = new[] { display },
                IsMuted = false,
                IsGroup = false,
                PeerId = message.SenderId,
                LastMessagePreview = message.Content,
                LastMessageAt = message.Timestamp,
                Status = PresenceStatus.Online
            });
            Rooms = list;
        }

        if (!string.Equals(message.SenderId, _options.UserId, StringComparison.OrdinalIgnoreCase))
        {
            room = Rooms.FirstOrDefault(roomItem => roomItem.Id == message.RoomId);
            message = message with
            {
                SenderName = room?.IsGroup == true
                    ? message.SenderName
                    : room?.Name ?? message.SenderName
            };
        }

        AddMessage(message, raiseToast: true);
        NotifyStateChanged();
    }

    private async Task JoinRoomsAsync(IEnumerable<string> roomIds)
    {
        if (IsMockMode)
        {
            return;
        }

        foreach (var roomId in roomIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (_joinedRooms.Add(roomId))
            {
                await _realtimeClient.JoinRoomAsync(roomId);
            }
        }
    }

    private async Task LoadCachedMessagesAsync()
    {
        foreach (var room in Rooms)
        {
            await LoadCachedMessagesForRoomAsync(room.Id);
        }
    }

    private async Task LoadCachedMessagesForRoomAsync(string roomId)
    {
        try
        {
            var cached = await _localStorage.GetItemAsync<List<ChatMessage>>(MessagesKeyPrefix + roomId);
            if (cached is null || cached.Count == 0)
            {
                return;
            }

            if (!_messagesByRoom.TryGetValue(roomId, out var messages))
            {
                _messagesByRoom[roomId] = cached;
                return;
            }

            var existingIds = new HashSet<string>(messages.Select(m => m.Id));
            foreach (var item in cached)
            {
                if (existingIds.Add(item.Id))
                {
                    messages.Add(item);
                }
            }

            _messagesByRoom[roomId] = messages
                .OrderBy(m => m.Timestamp)
                .TakeLast(MaxCachedMessages)
                .ToList();
        }
        catch
        {
            // Ignore cache failures.
        }
    }

    private async Task CacheMessagesAsync(string roomId)
    {
        try
        {
            if (!_messagesByRoom.TryGetValue(roomId, out var messages))
            {
                return;
            }

            var trimmed = messages
                .OrderBy(m => m.Timestamp)
                .TakeLast(MaxCachedMessages)
                .ToList();

            await _localStorage.SetItemAsync(MessagesKeyPrefix + roomId, trimmed);
        }
        catch
        {
            // Ignore cache failures.
        }
    }

    private IReadOnlyList<Room> NormalizeRooms(IReadOnlyList<Room> rooms)
    {
        if (rooms.Count == 0)
        {
            return rooms;
        }

        return rooms.Select(room =>
            room.IsGroup || string.IsNullOrWhiteSpace(room.PeerId)
                ? room
                : room with { Id = BuildDirectRoomId(_options.UserId, room.PeerId) }).ToList();
    }

    private IReadOnlyList<Room> EnsurePeerRoom(IReadOnlyList<Room> rooms)
    {
        if (string.IsNullOrWhiteSpace(_options.PeerId))
        {
            return rooms;
        }

        var peerId = _options.PeerId.Trim();
        if (rooms.Any(room => string.Equals(room.PeerId, peerId, StringComparison.OrdinalIgnoreCase)))
        {
            return rooms;
        }

        var name = string.IsNullOrWhiteSpace(_options.PeerName)
            ? peerId.Split('@')[0]
            : _options.PeerName;

        var list = rooms.ToList();
        list.Insert(0, new Room
        {
            Id = BuildDirectRoomId(_options.UserId, peerId),
            Name = name,
            Description = "Direct message",
            MemberCount = 2,
            Participants = new[] { name },
            IsMuted = false,
            IsGroup = false,
            PeerId = peerId,
            LastMessagePreview = "Start a conversation",
            LastMessageAt = null,
            Status = PresenceStatus.Online
        });

        return list;
    }

    private static string BuildDirectRoomId(string userId, string peerId)
    {
        var pair = new[] { userId.Trim().ToLowerInvariant(), peerId.Trim().ToLowerInvariant() }
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return $"direct:{pair[0]}:{pair[1]}";
    }

    private void HandleRealtimeNotification(NotificationItem notification)
    {
        AddNotification(notification, addToast: true);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_mockLoopCts is not null)
        {
            _mockLoopCts.Cancel();
            _mockLoopCts.Dispose();
        }

        if (!IsMockMode)
        {
            await _realtimeClient.DisconnectAsync();
        }
    }
}
