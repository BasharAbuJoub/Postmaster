# 📬 Postmaster

A robust outbox-backed HTTP dispatcher for .NET. Postmaster implements the transactional outbox pattern to guarantee reliable HTTP message delivery with automatic retries, channel-based ordering, and crash recovery.

## ✨ Features

- 📬 **Reliable delivery** — messages are persisted before sending; no fire-and-forget
- 🔁 **Automatic retries** with exponential backoff
- 🔀 **Channel ordering** — sequential delivery per channel, concurrent for channelless messages
- ⏰ **Scheduled delivery** — delay messages to a future time
- 💀 **Dead message handling** — exhausted messages are isolated and manageable
- 🔗 **Correlation ID** — every message carries an `X-Correlation-Id` header automatically
- 🏷️ **Metadata** — attach and search arbitrary data on messages
- 🛠️ **Management API** — query, reset, cancel, and inspect messages via `IOutboxManager`
- 📊 **Statistics** — built-in success rate and status breakdown
- 🖥️ **Embedded dashboard** — dark-themed web UI for monitoring and managing messages
- 🔌 **Extensible** — plug in any storage backend, processor host, or event handler
- ⚙️ **EF Core provider** — batteries-included persistence with migration support
- 🪝 **Hangfire integration** — drive the processor with a Hangfire recurring job

## 📦 Packages

| Package | Description |
|---------|-------------|
| `Postmaster` | Core library — abstractions, processor, publisher |
| `Postmaster.EntityFrameworkCore` | Entity Framework Core storage provider |
| `Postmaster.Dashboard` | Embedded web dashboard |
| `Postmaster.Hangfire` | Hangfire recurring job integration |

## 💡 The Outbox Pattern

Instead of sending HTTP requests directly (which can fail silently if your process crashes mid-request), you write the request to a database table in the same transaction as your business logic. A background processor then reads and dispatches the requests reliably, with retries on failure.

```
Your code                    Database              Background processor
─────────────────────────    ──────────────────    ────────────────────────────
EnqueueAsync(request)   ──►  OutboxMessages   ──►  Send HTTP request
(same transaction as         (persisted)           Retry on failure
your business logic)                               Mark succeeded/failed/dead
```

## ⚙️ Installation

```bash
dotnet add package Postmaster.EntityFrameworkCore
```

## 🚀 Setup

### 1. Configure your DbContext

Call `UsePostmaster()` inside `OnModelCreating`:

```csharp
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UsePostmaster(); // default table name: "OutboxMessages"
        // or
        modelBuilder.UsePostmaster(tableName: "MyOutbox");
    }
}
```

### 2. Add a migration

```bash
dotnet ef migrations add AddPostmaster
dotnet ef database update
```

### 3. Register services

```csharp
builder.Services.AddPostmaster(postmaster =>
{
    postmaster.UseEntityFrameworkCore<AppDbContext>();
    postmaster.UseBackgroundService();
});
```

With custom options:

```csharp
builder.Services.AddPostmaster(postmaster =>
{
    postmaster.Configure(options =>
    {
        options.BatchSize = 20;
        options.DefaultMaxRetryCount = 5;
        options.PollingInterval = TimeSpan.FromSeconds(10);
        options.ProcessingTimeout = TimeSpan.FromMinutes(5);
    });
    postmaster.UseEntityFrameworkCore<AppDbContext>();
    postmaster.UseBackgroundService();
});
```

## 📨 Publishing Messages

Inject `IOutboxPublisher` and call `EnqueueAsync`:

```csharp
public class OrderService
{
    private readonly IOutboxPublisher _publisher;

    public OrderService(IOutboxPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(Order order)
    {
        // ... save order to database in same transaction ...

        await _publisher.EnqueueAsync(new OutboxRequest
        {
            Url = "https://api.example.com/webhooks/order-placed",
            Method = "POST",
            Payload = JsonSerializer.Serialize(order),
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer my-token"
            },
        });
    }
}
```

### Bulk enqueue

```csharp
await _publisher.EnqueueBulkAsync(requests);
```

### ⏰ Scheduling

Delay delivery until a future time:

```csharp
await _publisher.EnqueueAsync(new OutboxRequest
{
    Url = "https://api.example.com/reminders",
    Method = "POST",
    Payload = JsonSerializer.Serialize(reminder),
    ScheduleAt = DateTime.UtcNow.AddHours(24),
});
```

## 🔀 Channels

Channels enforce ordered, sequential delivery. Messages in the same channel are processed one at a time, in the order they were enqueued. Messages without a channel are processed concurrently.

```csharp
// These three messages will be delivered in order, one at a time
await _publisher.EnqueueAsync(new OutboxRequest { Channel = "user-123", ... });
await _publisher.EnqueueAsync(new OutboxRequest { Channel = "user-123", ... });
await _publisher.EnqueueAsync(new OutboxRequest { Channel = "user-123", ... });

// This message is independent and processed concurrently with others
await _publisher.EnqueueAsync(new OutboxRequest { ... }); // no channel
```

### ⛔ Channel blocking

A channel is automatically blocked when any of its messages enters the `Dead` state. No further messages on that channel are processed until the dead message is reset or cancelled via `IOutboxManager`. This prevents delivering later messages before an earlier one has been resolved.

## 🔁 Retry Behaviour

Failed messages are retried with exponential backoff:

| Retry | Delay |
|-------|-------|
| 1st | 2 minutes |
| 2nd | 4 minutes |
| 3rd | 8 minutes |
| ... | 2^n minutes |

After `MaxRetryCount` retries the message is marked `Dead`. The default is 3 retries, configurable per-message:

```csharp
await _publisher.EnqueueAsync(new OutboxRequest
{
    Url = "...",
    Method = "POST",
    MaxRetryCount = 10, // override default
});
```

## 📊 Message Status

| Status | Description |
|--------|-------------|
| `Pending` | ⏳ Waiting to be processed |
| `Processing` | ⚙️ Currently being dispatched |
| `Succeeded` | ✅ HTTP request completed with a 2xx response |
| `Failed` | ❌ HTTP request failed, scheduled for retry |
| `Dead` | 💀 Exhausted all retries, requires manual intervention |
| `Cancelled` | 🚫 Manually cancelled before processing |

## 🛠️ Managing Messages

Inject `IOutboxManager` to query and manage messages:

```csharp
// Get a single message
var detail = await _manager.GetByIdAsync(messageId);

// Query with filters
var page = await _manager.GetAsync(new OutboxQuery
{
    Status = OutboxMessageStatus.Dead,
    Channel = "user-123",
    From = DateTime.UtcNow.AddDays(-7),
    SortBy = OutboxSortBy.CreatedAt,
    Ascending = false,
    Page = 1,
    PageSize = 20,
});

// Search by correlation ID
var page = await _manager.GetAsync(new OutboxQuery
{
    CorrelationId = "my-correlation-id",
});

// Search by metadata substring
var page = await _manager.GetAsync(new OutboxQuery
{
    MetadataContains = "order-42",
});

// Get statistics
var stats = await _manager.GetStatsAsync();
Console.WriteLine($"Success rate: {stats.SuccessRate:F1}%");

// Reset a dead message for reprocessing
await _manager.ResetAsync(messageId);

// Reset all failed/dead messages in a channel
await _manager.ResetChannelAsync("user-123");

// Cancel a pending message
await _manager.CancelAsync(messageId);
```

## 🔗 Correlation

Every message automatically gets a unique `CorrelationId` (a GUID) at enqueue time. You can also provide your own:

```csharp
await _publisher.EnqueueAsync(new OutboxRequest
{
    Url = "...",
    Method = "POST",
    CorrelationId = myTraceId, // explicit override
});
```

The correlation ID is forwarded as an `X-Correlation-Id` header on every outgoing HTTP request.

## 🏷️ Metadata

Attach arbitrary string metadata to a message for filtering or debugging:

```csharp
await _publisher.EnqueueAsync(new OutboxRequest
{
    Url = "...",
    Method = "POST",
    Metadata = JsonSerializer.Serialize(new { OrderId = 42, UserId = "user-123" }),
});

// Later, search by metadata content
var page = await _manager.GetAsync(new OutboxQuery
{
    MetadataContains = "OrderId",
});
```

## 🖥️ Dashboard

The dashboard provides a real-time web UI for monitoring and managing outbox messages.

```bash
dotnet add package Postmaster.Dashboard
```

```csharp
app.UsePostmasterDashboard("/postmaster");
```

With authentication:

```csharp
app.UsePostmasterDashboard("/postmaster", options =>
{
    options.Username = "admin";
    options.Password = "your-password";
});
```

The dashboard is fully self-contained — all assets (Tailwind CSS, Alpine.js) are embedded in the assembly and served locally, with no external CDN requests. This makes it suitable for air-gapped environments.

**Features:**
- Filter messages by status, channel, date range, correlation ID, and metadata
- Inspect full request/response details, headers, payload, and error messages
- Reset or cancel individual messages
- Live stats with success rate and average elapsed time

## 🪝 Hangfire Integration

Replace the built-in `BackgroundService` with a Hangfire recurring job that fires on the configured `PollingInterval`.

```bash
dotnet add package Postmaster.Hangfire
```

```csharp
// Register Hangfire with your storage backend
builder.Services.AddHangfire(config => config
    .UseRecommendedSerializerSettings()
    .UseConsole()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// Use Hangfire instead of the built-in BackgroundService
builder.Services.AddPostmaster(postmaster =>
{
    postmaster.UseEntityFrameworkCore<AppDbContext>();
    postmaster.UseHangfire(); // replaces UseBackgroundService()
    postmaster.Configure(options =>
    {
        options.PollingInterval = TimeSpan.FromSeconds(30);
    });
});

// Mount the Hangfire dashboard
app.UseHangfireDashboard("/hangfire");
```

Each job execution drains the queue fully and logs per-message outcomes (ID, status, elapsed, correlation ID, channel, metadata) directly to the Hangfire job console via `Hangfire.Console`.

> **Note:** `PollingInterval` is converted to a cron expression. Values under 60 seconds are clamped to every minute, which is cron's minimum granularity.

## 🔔 Event Handlers

Implement `IOutboxEventHandler` to observe every dispatched message — useful for metrics, alerting, or audit logging. Handlers are resolved from DI per message scope, so scoped dependencies are supported.

```csharp
public class MetricsEventHandler : IOutboxEventHandler
{
    private readonly IMetrics _metrics;

    public MetricsEventHandler(IMetrics metrics) => _metrics = metrics;

    public Task OnDispatchedAsync(OutboxDispatchResult result, CancellationToken ct = default)
    {
        _metrics.RecordDispatch(result.Succeeded, result.ElapsedMs ?? 0);
        return Task.CompletedTask;
    }
}
```

Register in DI:

```csharp
builder.Services.AddScoped<IOutboxEventHandler, MetricsEventHandler>();
```

Multiple handlers can be registered — all are called after each message is persisted. A faulting handler is logged and skipped so it cannot affect other handlers or message processing.

## ⚙️ Configuration Reference

| Option | Default | Description |
|--------|---------|-------------|
| `BatchSize` | `10` | Number of messages acquired and processed per cycle |
| `DefaultMaxRetryCount` | `3` | Default retry limit for messages that don't specify their own |
| `PollingInterval` | `30s` | How long the processor waits when there are no pending messages |
| `ProcessingTimeout` | `10min` | How long a message can stay in `Processing` before being recovered |

## 🛡️ Delivery Guarantee

Postmaster guarantees **at-least-once delivery**. In normal operation no message is delivered twice. The rare exception is if a worker holds a message in `Processing` longer than `ProcessingTimeout` — the recovery sweep will reset it and another worker may pick it up while the original request is still in flight.

To protect against this, make your receiving endpoints **idempotent** — check whether the message has already been processed using the `X-Correlation-Id` header or your own business key.

## 🔌 Extending Postmaster

### Custom storage provider

Implement `IOutboxStore` and register it on the builder:

```csharp
builder.Services.AddPostmaster(postmaster =>
{
    postmaster.Services.AddScoped<IOutboxStore, MyCustomOutboxStore>();
    postmaster.UseBackgroundService();
});
```

### Custom processor host

Implement your own host and call `IOutboxProcessor.ProcessAsync` from your job:

```csharp
public class CustomProcessorJob
{
    private readonly IOutboxProcessor _processor;

    public CustomProcessorJob(IOutboxProcessor processor)
    {
        _processor = processor;
    }

    public async Task RunAsync()
    {
        await _processor.ProcessAsync();
    }
}
```

Register without `UseBackgroundService`:

```csharp
builder.Services.AddPostmaster(postmaster =>
{
    postmaster.UseEntityFrameworkCore<AppDbContext>();
    // no UseBackgroundService() — your custom host drives the processor instead
});
```
