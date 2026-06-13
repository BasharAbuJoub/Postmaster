# Code Review: Postmaster HttpOutbox Library

Overall the design is clean — good separation between Core and EFCore, sensible abstractions, and the channel-blocking concept is smart. Below are issues ordered by severity.

---

## Critical

### 1. `AcquireAsync` is not race-condition safe (`EfCoreOutboxStore.cs:40`)

`RepeatableRead` prevents dirty reads but does **not** prevent two concurrent workers from seeing the same `Pending` rows and both marking them `Processing`. On PostgreSQL (MVCC), this is a real risk. The standard solution is `SELECT FOR UPDATE SKIP LOCKED`, which requires raw SQL but is the correct primitive for job queues:

```sql
SELECT * FROM "OutboxMessages"
WHERE "Status" IN ('Pending','Failed') AND "NextAttemptAt" <= NOW()
ORDER BY "CreatedAt"
LIMIT @batchSize
FOR UPDATE SKIP LOCKED
```

### 2. Null-channel messages are incorrectly serialized (`EfCoreOutboxStore.cs:59`)

```csharp
var messages = candidates
    .GroupBy(x => x.Channel)  // null is a valid group key
    .Select(g => g.First())   // only ONE null-channel message per batch
    .Take(batchSize)
    .ToList();
```

Every message without a channel shares the `null` group, so only one gets processed per batch cycle. Null-channel messages should be concurrent; only named channels need the "one at a time" guarantee. Fix: only apply `GroupBy` to the non-null channel messages, then combine with all null-channel candidates.

### 3. `AcquireAsync` loads ALL candidates into memory (`EfCoreOutboxStore.cs:52`)

`ToListAsync()` on the candidates query fetches every pending message before the in-memory `.GroupBy().Take(batchSize)`. On a large table this is a full table scan into RAM. The `Take(batchSize)` should be pushed into SQL.

### 4. `ResetAsync` resets messages of any status (`EfCoreOutboxManager.cs:147`)

```csharp
await Messages.Where(x => x.Id == id).ExecuteUpdateAsync(ApplyResetSetters, ct);
```

There is no status filter. Calling this on a `Processing` message causes double-processing; calling it on `Succeeded` re-delivers an already-sent message. It should restrict to `Failed | Dead | Cancelled`.

---

## Significant

### 5. `HasPendingAsync` doesn't include `Failed`-ready messages (`EfCoreOutboxStore.cs:20`)

```csharp
x.Status == OutboxMessageStatus.Pending && x.NextAttemptAt <= DateTime.UtcNow
```

`Failed` messages whose `NextAttemptAt` has passed are also ready to process, but this check misses them. The host goes idle, then wakes 30 seconds later to process them. The condition should mirror the `AcquireAsync` filter.

### 6. `OutboxStats` is missing `Cancelled` and its `Total`/`SuccessRate` are misleading (`EfCoreOutboxManager.cs:123`)

`Total = grouped.Sum(...)` silently includes `Cancelled` in the denominator of `SuccessRate` but there is no `Cancelled` property in `OutboxStats`. Users get a success rate calculated against a total they cannot verify.

### 7. Idle log is `LogInformation`, will spam production logs (`OutboxProcessorHost.cs:34`)

Every 30 seconds the host logs "processor is idle..." at `Information`. That is ~2,880 lines/day doing nothing. Change to `LogDebug`.

### 8. Content-Type hardcoded to `application/json` (`OutboxProcessor.cs:112`)

```csharp
request.Content = new StringContent(message.Payload, Encoding.UTF8, "application/json");
```

Callers sending XML, form data, etc. get a wrong `Content-Type`. The header should be driven by whatever is in `message.Headers`, with `application/json` only as a fallback when no `Content-Type` header is provided.

### 9. `PostmasterOptions` injected as a raw singleton class (`ServiceCollectionExtensions.cs:17`)

Using `services.AddSingleton(options)` makes the config impossible to bind from `appsettings.json`. The standard .NET pattern is `IOptions<PostmasterOptions>`. This is the pattern library consumers will expect.

---

## Design / API Surface

### 10. `OutboxRequest` record uses `set` instead of `init`

All properties except `Headers` use `{ get; set; }`, making a `sealed record` mutable after construction. They should all use `{ get; init; }` for consistency with record semantics.

### 11. `IOutboxStore` and `IOutboxProcessor` are `public` but are implementation details

`IOutboxStore` exposes `AcquireAsync`, `SaveBulkAsync`, `UpdateBulkAsync` — low-level plumbing that library consumers should never implement or depend on directly. Both interfaces should be `internal`.

### 12. `OutboxQuery.SortBy` is a magic string with no validation

An invalid value silently falls through to the default sort. An enum like `OutboxSortBy { CreatedAt, Status, ElapsedMs }` is safer, more discoverable, and prevents runtime surprises.

### 13. `OutboxMessage` entity has no `required` annotations and nullable is disabled

Properties like `Url` and `Method` are `string` (non-nullable) but the parameterless constructor enforces nothing. Enable `<Nullable>enable</Nullable>` in both `.csproj` files and mark required properties as `required`.

### 14. `OutboxMessageResult` and `OutboxMessageSummary` use mutable `{ get; set; }`

These are read-only DTOs. They should use `{ get; init; }` or be `record` types. `OutboxMessageResult` in particular should be a `sealed record`.

---

## Minor

### 15. Dead-channel blocking is undocumented (`EfCoreOutboxStore.cs:46`)

A channel is blocked if any message in it is `Dead`. This is non-obvious — a user who doesn't know this will wonder why new messages on a channel stop processing after a single dead message. Add an XML doc comment on the interface or a README note explaining the behaviour.

### 16. No stuck-`Processing` recovery

If the host process crashes while messages are `Processing`, they remain `Processing` forever and are never retried. A common mitigation is a background check that resets `Processing` messages older than a configurable timeout (e.g., 10 minutes) back to `Pending`.

### 17. `.csproj` missing `<Nullable>enable</Nullable>` and `<GenerateDocumentationFile>true</GenerateDocumentationFile>`

Both are important before a NuGet release. Nullable gives compile-time safety; the XML doc file makes IntelliSense descriptions show up for consumers.

---

## Summary

| # | File | Severity | Status |
|---|------|----------|--------|
| 1 | `EfCoreOutboxStore.cs:40` | Critical — double-processing | ✅ Fixed — `LockedBy` atomic claim replaces `RepeatableRead` transaction |
| 2 | `EfCoreOutboxStore.cs:59` | Critical — null-channel starvation | ✅ Fixed — two pools merged by age, neither can starve the other |
| 3 | `EfCoreOutboxStore.cs:52` | Critical — full-table memory load | ✅ Fixed — small projection (Id/Channel/CreatedAt only), null-channel bounded by `batchSize` |
| 4 | `EfCoreOutboxManager.cs:147` | Critical — double-delivery on reset | ✅ Fixed — `ResetAsync` blocks `Processing` only; `Succeeded` intentionally allowed for re-triggering |
| 5 | `EfCoreOutboxStore.cs:20` | Significant — idle when retries are ready | ✅ Fixed — `HasPendingAsync` now checks `Failed` messages too |
| 6 | `EfCoreOutboxManager.cs:123` | Significant — stats correctness | ✅ Fixed — `Cancelled` added; `SuccessRate` now calculated against attempted messages only (`Succeeded + Failed + Dead`) |
| 7 | `OutboxProcessorHost.cs:34` | Significant — log spam | ✅ Fixed — idle log changed to `LogDebug` |
| 8 | `OutboxProcessor.cs:112` | Significant — wrong Content-Type | ✅ Fixed — `Content-Type` read from headers if provided, falls back to `application/json` |
| 9 | `ServiceCollectionExtensions.cs:17` | Significant — config pattern | ✅ Fixed — switched to `IOptions<PostmasterOptions>` across all consumers |
| 10 | `OutboxRequest.cs` | API — mutable record properties | ✅ Fixed — all properties changed to `init` |
| 11 | `IOutboxStore.cs`, `IOutboxProcessor.cs` | API — public implementation details | ✅ Fixed — both kept `public`; they are intentional extension points for `Postmaster.Redis`, `Postmaster.Hangfire`, etc. |
| 12 | `OutboxQuery.cs` | API — magic string sort | ✅ Fixed — `SortBy` changed to `OutboxSortBy` enum |
| 13 | `OutboxMessage.cs`, both `.csproj` | API — no nullability | ✅ Fixed — `Nullable` enabled in both projects; required properties marked `required`, optional ones marked `string?` |
| 14 | `OutboxMessageResult.cs`, `OutboxMessageSummary.cs` | API — mutable DTOs | ✅ Fixed — `OutboxMessageResult` converted to `sealed record` with `init`; `OutboxMessageSummary` already used `init` |
| 15 | `EfCoreOutboxStore.cs` | Minor — Dead-channel blocking undocumented | ⚠️ Open |
| 16 | `OutboxProcessor.cs` | Minor — no stuck-`Processing` recovery | ✅ Fixed — `RecoverStuckMessagesAsync` runs each cycle with `LockedBy` ownership guard |
| 17 | `.csproj` files | Minor — missing `Nullable` and XML doc generation | ✅ Fixed — addressed as part of #13 |

Items 4, 6–9 should be addressed before release. Items 10–17 are polish.
