using RealTimerBuzz.Models;

namespace RealTimerBuzz.Services;

public static class MockDataFactory
{
    private static readonly string[] GroupNames =
    [
        "Product Ops",
        "Design Sprint",
        "Realtime Lab",
        "Launch Crew",
        "Growth Guild",
        "Support Hub"
    ];

    private static readonly string[] People =
    [
        "Ava Castillo",
        "Luca Moreno",
        "Mina Park",
        "Noah Silva",
        "Zoe Herrera",
        "Kai Nakamura",
        "Iris Volkov",
        "Theo Brandt",
        "Sora Lee",
        "Maya Ortega"
    ];

    private static readonly string[] Snippets =
    [
        "Shipping the new flow in 30 min.",
        "Heads up: latency spikes in EU.",
        "Can we align on the onboarding copy?",
        "Final mockups are in Figma.",
        "We should pin the incident summary.",
        "Kudos for the hotfix — clean work.",
        "Drafted the release notes.",
        "Notifications are trending +18%.",
        "Let us sync on the metrics deck.",
        "I can pick up QA if needed.",
        "Mentions are on fire.",
        "Any blockers for today?"
    ];

    private static readonly Random Random = new(42);

    public static IReadOnlyList<Room> BuildRooms()
    {
        var contacts = People.Select((name, index) => new Room
        {
            Id = $"contact-{index + 1}",
            Name = name,
            Description = "Direct message",
            MemberCount = 2,
            Participants = new[] { name },
            IsMuted = Random.NextDouble() > 0.85,
            IsGroup = false,
            PeerId = $"{name.Split(' ')[0].ToLowerInvariant()}@rtb.local",
            LastMessagePreview = Snippets[Random.Next(Snippets.Length)],
            LastMessageAt = DateTimeOffset.Now.AddMinutes(-Random.Next(2, 90)),
            Status = Random.NextDouble() switch
            {
                > 0.7 => PresenceStatus.Online,
                > 0.4 => PresenceStatus.Away,
                _ => PresenceStatus.Offline
            }
        }).ToList();

        var groups = GroupNames.Select((name, index) => new Room
        {
            Id = $"group-{index + 1}",
            Name = name,
            Description = "Group chat",
            MemberCount = Random.Next(4, 18),
            Participants = People.OrderBy(_ => Random.Next()).Take(4).ToArray(),
            IsMuted = Random.NextDouble() > 0.8,
            IsGroup = true,
            PeerId = null,
            LastMessagePreview = Snippets[Random.Next(Snippets.Length)],
            LastMessageAt = DateTimeOffset.Now.AddMinutes(-Random.Next(5, 140)),
            Status = PresenceStatus.Online
        }).ToList();

        return contacts.Concat(groups).OrderByDescending(room => room.LastMessageAt).ToList();
    }

    public static List<ChatMessage> BuildMessages(string roomId, string roomName)
    {
        var start = DateTimeOffset.Now.AddMinutes(-90);
        var messages = new List<ChatMessage>();

        for (var i = 0; i < 42; i++)
        {
            var isMine = i % 7 == 0;
            var sender = isMine ? "You" : People[Random.Next(People.Length)];
            messages.Add(new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                RoomId = roomId,
                SenderId = isMine ? "me" : sender.ToLowerInvariant().Replace(" ", string.Empty),
                SenderName = sender,
                Content = Snippets[Random.Next(Snippets.Length)] + (i % 6 == 0 ? " " + roomName + " sync." : string.Empty),
                Timestamp = start.AddMinutes(i * 2 + Random.Next(0, 2)),
                IsMine = isMine
            });
        }

        return messages;
    }

    public static IReadOnlyList<NotificationItem> BuildNotifications(IReadOnlyList<Room> rooms)
    {
        var roomIds = rooms.Select(room => room.Id).ToArray();
        return Enumerable.Range(0, 9).Select(index => new NotificationItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = index % 3 == 0 ? "Mention" : "New message",
            Body = Snippets[Random.Next(Snippets.Length)],
            Timestamp = DateTimeOffset.Now.AddMinutes(-index * 4),
            RoomId = roomIds.Length > 0 ? roomIds[Random.Next(roomIds.Length)] : null,
            Kind = index % 3 == 0 ? NotificationKind.Mention : NotificationKind.Message,
            IsRead = index > 2
        }).ToList();
    }

    public static ChatMessage BuildIncomingMessage(string roomId, string roomName)
    {
        var sender = People[Random.Next(People.Length)];
        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            RoomId = roomId,
            SenderId = sender.ToLowerInvariant().Replace(" ", string.Empty),
            SenderName = sender,
            Content = Snippets[Random.Next(Snippets.Length)] + (Random.NextDouble() > 0.7 ? $" #{roomName}" : string.Empty),
            Timestamp = DateTimeOffset.Now,
            IsMine = false
        };
    }
}
