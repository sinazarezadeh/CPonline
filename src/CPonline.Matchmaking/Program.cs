using CPonline.Matchmaking.Rooms;
using CPonline.Matchmaking.Sockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IRoomStore, RoomStore>();
builder.Services.AddSingleton<SessionSocketHandler>();

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Map("/session", async (HttpContext context, SessionSocketHandler handler) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await handler.HandleAsync(socket, context.RequestAborted);
});

app.Run();

// Exposed for CPonline.Matchmaking.Tests' WebApplicationFactory<Program>. Top-level statements
// implicitly generate a `Program` class in the global namespace, so this partial declaration
// must stay unwrapped here to merge with it rather than declaring an unrelated empty type.
public partial class Program;
