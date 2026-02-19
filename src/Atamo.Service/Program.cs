using Atamo.Hub;
using Atamo.SDK;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<InMemoryHub>();
builder.Services.AddSingleton<IHubReceiver>(sp => sp.GetRequiredService<InMemoryHub>());
builder.Services.AddSingleton<IHubControl>(sp => sp.GetRequiredService<InMemoryHub>());

var app = builder.Build();

app.MapPost("/events", async (EventMessage msg, IHubReceiver hub) =>
{
    var key = await hub.SubmitEventAsync(msg);
    return Results.Ok(new { EventKey = key.Key });
});

app.MapGet("/health", async (IHubControl ctrl) =>
{
    var h = await ctrl.GetHealthAsync();
    return Results.Ok(h);
});

app.Run();
