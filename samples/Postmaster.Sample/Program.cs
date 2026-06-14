using Microsoft.EntityFrameworkCore;
using Postmaster;
using Postmaster.Sample;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddPostmaster(postmaster =>
{
    postmaster.UseEntityFrameworkCore<AppDbContext>();
    postmaster.UseBackgroundService();
    postmaster.Configure(options =>
    {
        options.PollingInterval = TimeSpan.FromSeconds(10);
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UsePostmasterDashboard("/postmaster", options =>
{
    options.Username = "admin";
    options.Password = "password";
});

app.MapPost("/outbox", async (IOutboxPublisher publisher, CancellationToken ct) =>
{
    var message = await publisher.EnqueueAsync(new OutboxRequest()
    {
        Url = "https://httpbin.org/post",
        Method = "POST",
        Payload = JsonSerializer.Serialize(new
        {
            Message = "Hello, Postmaster!"
        })
    }, ct);

    return Results.Ok(new
    {
        MessageId = message.Id,
        CreatedAt = message.CreatedAt,
        Status = "Message enqueued successfully"
    });
});

app.Run();