using RealTimerBuzz.Components;
using RealTimerBuzz.Models;
using RealTimerBuzz.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var chatOptions = builder.Configuration.GetSection("Chat").Get<ChatOptions>() ?? new ChatOptions();
builder.Services.AddSingleton(chatOptions);
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<BrowserNotificationService>();
builder.Services.AddScoped<ChatRealtimeClient>();
builder.Services.AddScoped<ChatStateService>();
builder.Services.AddHttpClient<ChatApiClient>(client => client.BaseAddress = new Uri(chatOptions.ApiBaseUrl));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
