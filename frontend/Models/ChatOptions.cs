namespace RealTimerBuzz.Models;

public enum ChatMode
{
    Mock,
    Live
}

public sealed class ChatOptions
{
    public ChatMode Mode { get; init; } = ChatMode.Mock;
    public string ApiBaseUrl { get; init; } = "https://localhost:5001";
    public string SignalRHubUrl { get; init; } = "https://localhost:5001/hubs/chat";
    public string UserId { get; init; } = "me@rtb.local";
    public string? PeerId { get; init; }
    public string? PeerName { get; init; }
}

public enum ThemePreference
{
    Dark,
    Light,
    System
}

public sealed class UserPreferences
{
    public ThemePreference Theme { get; init; } = ThemePreference.Dark;
    public bool ReduceMotion { get; init; }
}
