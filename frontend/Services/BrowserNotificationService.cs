using Microsoft.JSInterop;

namespace RealTimerBuzz.Services;

public sealed class BrowserNotificationService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserNotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<string> RequestPermissionAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("rtb.notifications.requestPermission");
    }

    public async ValueTask<string> GetPermissionAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("rtb.notifications.permission");
    }

    public async ValueTask NotifyAsync(string title, string body)
    {
        await _jsRuntime.InvokeVoidAsync("rtb.notifications.notify", title, body);
    }

    public async ValueTask<bool> IsFocusedAsync()
    {
        return await _jsRuntime.InvokeAsync<bool>("rtb.notifications.isFocused");
    }
}
