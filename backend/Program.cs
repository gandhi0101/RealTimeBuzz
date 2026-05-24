using MongoDB.Driver;
using RealTimeBuzz.Hubs;
using RealTimeBuzz.Models;
using RealTimeBuzz.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
builder.Services.AddSingleton(mongoOptions);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoOptions.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.Database));
builder.Services.AddSingleton<ChatMessageRepository>();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}
app.UseCors("frontend");
app.UseWebSockets();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/api/rooms", () => Results.Ok(Array.Empty<object>()));

app.MapGet("/api/rooms/{roomId}/messages", async (string roomId, int? limit, DateTimeOffset? before, ChatMessageRepository repo) =>
{
    var take = limit is { } value ? Math.Clamp(value, 1, 200) : 50;
    var messages = await repo.GetByRoomAsync(roomId, take, before);
    var dtos = messages
        .OrderBy(x => x.CreatedAt)
        .Select(x => new ChatMessageDto(
            x.MessageId,
            x.RoomId,
            x.SenderId,
            x.RecipientId,
            x.Content,
            x.CreatedAt))
        .ToList();
    return Results.Ok(dtos);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
