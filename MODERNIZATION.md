# HearingsMessenger — Modernization Plan & Notes

HCAD's hearings messaging component. Derived from **TinyMessenger** (.NET Framework 4.0 /
C# 4, VS2010 era), modernized to current .NET LTS and extended with a one-way broadcast
layer for domain-joined Windows 11 workstations.

Build with: `dotnet build src/HearingsMessenger/HearingsMessenger.sln`
Test with:  `dotnet test  src/HearingsMessenger/HearingsMessenger.sln`

> **Verification status (2026-07-13):** this codebase is **fully build- and test-verified**
> on the **.NET 10 SDK (10.0.301)**. The solution compiles with **0 warnings / 0 errors**
> under `TreatWarningsAsErrors`, nullable, and XML-doc enforcement, and the complete test
> suite — **60 tests, all passing** — runs green via `dotnet test`. Coverage spans hub
> semantics (all 8 Subscribe overloads, filters, proxies, unsubscribe/token-dispose, error
> handlers, polymorphic delivery), weak-reference collection behavior (sync + async),
> PublishAsync/cancellation, broadcast fan-out/failure isolation/local echo/JSON
> round-trip, HttpAgentBroadcastTransport best-effort behavior, and end-to-end DI wiring.
>
> Package restore (`Microsoft.Extensions.*` 10.0.0; test packages MSTest 3.6.4,
> Moq 4.20.72, Microsoft.NET.Test.Sdk 17.12.0) resolves from nuget.org via the repo-local
> `src/HearingsMessenger/nuget.config`, so no machine-wide NuGet configuration is required.

> Targets `net10.0` (current LTS); change one line in each `.csproj` to `net8.0` if your
> agents are on the previous LTS.

---

## 0. Naming

- **Project/assembly/namespaces**: `HearingsMessenger` (solution, both csproj files,
  `AssemblyName`, `RootNamespace`, and all `namespace` declarations).
- **Type names**: retain their `TinyMessenger`/`TinyMessage` prefixes
  (`TinyMessengerHub`, `ITinyMessage`, `TinyMessageSubscriptionToken`, ...). They are the
  public API heritage of the original library and keeping them makes upstream
  documentation and examples directly applicable. Rename later with an IDE refactor if
  the team prefers.
- The solution file is classic `.sln` format (the .NET 10 SDK defaults to the new
  `.slnx`, which Visual Studio 2022 before 17.13 cannot open).

## 1. What changed and why

### Project system & tooling
| Before | After | Why |
| --- | --- | --- |
| VS2010-era MSBuild csproj, `TargetFrameworkVersion v4.0` | SDK-style csproj, `net10.0` LTS | Modern build, NuGet `PackageReference`, trimming/analyzer support |
| `Properties/AssemblyInfo.cs` | Deleted — generated from csproj metadata | One source of truth for version/description |
| Checked-in `Binaries/Moq.dll` (Moq 3.1) | NuGet `Moq 4.20.72` | No binaries in source control |
| MSTest v1 (`QualityTools`) | MSTest 3.6.4 + `Microsoft.NET.Test.Sdk` | Runs with `dotnet test` |
| Solution items (`.vsmdi`, `.testsettings`) | Removed | VS2010 test-run artifacts, obsolete |

### Language & type safety
- **Nullable reference types enabled** everywhere; `ITinyMessage.Sender` is now honestly `object?`.
- **File-scoped namespaces**, implicit usings, pattern matching (`message is TMessage typed`),
  `ArgumentNullException.ThrowIfNull`, records for internal pairs and public DTOs.
- `TreatWarningsAsErrors` + `GenerateDocumentationFile`: every public member is documented
  and the build is warning-clean.
- The single 830-line `TinyMessenger.cs` was split into focused files:
  `Messages.cs`, `SubscriberErrorHandler.cs`, `TinyMessageSubscriptionToken.cs`,
  `Subscriptions.cs`, `ITinyMessengerHub.cs`, `TinyMessengerHub.cs`.

### Async/await throughout
- `PublishAsync` now returns a `Task` and accepts a `CancellationToken` (checked between
  deliveries; cancellation propagates, subscriber exceptions go to the error handler).
- The old `PublishAsync(message, AsyncCallback)` overload is **removed, not preserved**:
  it relied on `Delegate.BeginInvoke`, which throws `PlatformNotSupportedException` on
  .NET Core/5+ — it could never work. Await the returned `Task` instead.
- New `SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task>, ...)` overloads
  (bare, +filter, +useStrongReferences, +both) give first-class async subscribers,
  including weak-reference variants.
- `ITinyMessageSubscription.Deliver` / `ITinyMessageProxy.Deliver` became `DeliverAsync`.
- Synchronous `Publish` is kept for compatibility: sync subscribers complete inline;
  async subscribers are awaited (blocking) — prefer `PublishAsync` when async
  subscribers may be registered.

### Legacy constructs replaced
- Untyped `WeakReference` → `WeakReference<T>` (`TryGetTarget`, no `IsAlive` race).
- Reflection-based unsubscribe in `TinyMessageSubscriptionToken.Dispose`
  (`GetMethod("Unsubscribe").MakeGenericMethod(...)`) → direct call to the
  non-generic `Unsubscribe(token)`, which removes by token identity. The token also
  gained a public `MessageType` property.
- `lock` + LINQ remove loop → `List.RemoveAll`.
- Weak-subscription **semantics preserved deliberately**: the hub holds a weak
  reference to the subscriber's delegate, so callers opting into weak subscriptions
  must keep the delegate alive (as before). This is the event-aggregator's
  memory-leak guard; documented on the interface.

### What was intentionally NOT changed
- All 8 original `Subscribe` overloads, message types (`GenericTinyMessage`,
  `CancellableGenericTinyMessage`), proxies, filters, error-handler behavior
  (swallow subscriber exceptions by default) — the library's conceptual
  simplicity and public API surface are preserved; existing call sites compile.

---

## 2. New: one-way broadcast layer (`HearingsMessenger.Broadcast`)

Purpose-built for the stated goal: fire-and-forget notifications to groups of
domain-joined Windows 11 workstations.

```
IBroadcastPublisher.PublishAsync(notification, machineGroup)
        │
        ├── (optional) local echo on ITinyMessengerHub   ← in-proc subscribers/audit
        └── fan-out to ALL registered IBroadcastTransport
                ├── HttpAgentBroadcastTransport   ← recommended, included
                ├── LoopbackBroadcastTransport    ← dev/test, included
                └── your gRPC / Service Bus / WinRM implementation
```

- **`BroadcastNotification`** — immutable `record` payload (Id, Title, Body, Severity,
  Sender, CreatedUtc/ExpiresUtc, Metadata dictionary for extension). Serializes cleanly
  with System.Text.Json (verified round-trip).
- **`MachineTarget` / `MachineGroup`** — records; `MachineGroup.FromHostNames(...)`
  helper. Materialize groups from AD OUs/groups in your directory-query layer.
- **`IBroadcastTransport`** — the extension point. Contract: one-way, best-effort;
  per-machine failures are logged, never thrown.
- **`BroadcastPublisher`** — fans out to every registered transport concurrently;
  never throws for delivery failures (honours cancellation).

### Recommended transport: HTTPS agent with Windows integrated auth
`HttpAgentBroadcastTransport` POSTs the JSON notification to a small agent on each
workstation (e.g. an ASP.NET Core minimal API Windows Service listening on
`https://+:7443/api/notifications` with Negotiate auth), which shows a toast via
Windows App SDK or republishes on the machine's local hub. Per-machine timeout
(default 10 s) via linked cancellation; concurrency capped by
`BroadcastOptions.MaxConcurrentSends` (default 16); Kerberos/Negotiate via
`CredentialCache.DefaultCredentials`; optionally accepts an injected `HttpClient`
(e.g. from `IHttpClientFactory`).

Why not the alternatives:
- **WinRM**: works but is a PowerShell-remoting surface most security baselines lock down.
- **MSMQ**: deprecated; not installed on Windows 11 by default.
- **gRPC**: excellent later upgrade — same agent model, swap the transport implementation
  without touching the library (that's what `IBroadcastTransport` is for).

Kerberos notes: use real AD host names (not IPs), issue agent TLS certs via AD CS
auto-enrollment, open the port via GPO/Intune, `setspn -S HTTP/<host> <account>` if the
agent runs under a custom account. Entra-joined-only machines: use client-cert auth
instead of Negotiate.

### Dependency injection
`TinyMessengerServiceCollectionExtensions`:

```csharp
builder.Services.AddTinyMessenger();                          // ITinyMessengerHub singleton
builder.Services.AddTinyMessengerBroadcast(o => o.DefaultPort = 7443);
// inject IBroadcastPublisher; add more transports:
builder.Services.AddSingleton<IBroadcastTransport, MyGrpcTransport>();
```

`AddTinyMessenger` respects a registered `ISubscriberErrorHandler`. Only
`Microsoft.Extensions.*` abstractions/Options packages are referenced — no container or
logging framework is forced on consumers.

---

## 3. Test changes
- All original tests preserved and modernized (`Assert.ThrowsException` instead of
  `[ExpectedException]`, `Assert.AreSame` instead of the buggy unasserted
  `Assert.ReferenceEquals`, async tests instead of `Thread.Sleep` wait loops).
- `AsyncCallback` tests replaced by `await PublishAsync` + async-subscriber tests
  (including cancellation and error-handler routing).
- **Semantics change reflected in one test**: `Dispose_WithValidHubReference_UnregistersWithHub`
  now verifies the **non-generic** `Unsubscribe(token)` call (the token no longer
  reflects over the generic overload).
- `GetTokenWithOutOfScopeMessenger` is `[MethodImpl(NoInlining)]` so the hub local is
  reliably collectable on modern JITs.
- New `BroadcastPublisherTests` cover group fan-out, multi-transport fan-out,
  transport-failure isolation, local echo on/off, cancellation, and null-argument checks.

## 4. Extending further
- **New transport**: implement `IBroadcastTransport`, register it in DI. Keep it one-way.
- **New payload fields**: extend `BroadcastNotification` with `init` properties or use
  `Metadata` for ad-hoc data (receiving agents ignore unknown fields).
- **Two-way needs (acks, targeting queries)**: out of scope by design — build a separate
  request/response service; don't bend the broadcast contract.
- **Receiving agent**: not included (it's a deployable service, not a library concern);
  the expected endpoint shape is documented in `HttpAgentBroadcastTransport`.
