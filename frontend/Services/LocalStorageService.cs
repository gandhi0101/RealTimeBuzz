using System.Text.Json;
using Microsoft.JSInterop;

namespace RealTimerBuzz.Services;

public sealed class LocalStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask SetItemAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await _jsRuntime.InvokeVoidAsync("rtb.localStorage.set", key, json);
    }

    public async ValueTask<T?> GetItemAsync<T>(string key)
    {
        var json = await _jsRuntime.InvokeAsync<string?>("rtb.localStorage.get", key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
