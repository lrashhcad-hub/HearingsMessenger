# HearingsMessenger

HCAD's hearings messaging component: a lightweight in-process **event aggregator / messenger
hub** (derived from [TinyMessenger](https://github.com/grumpydev/TinyMessenger)) modernized to
.NET 10 and extended with a one-way **broadcast layer** for fire-and-forget notifications to
groups of domain-joined Windows 11 workstations.

> **Status:** builds warning-clean under `TreatWarningsAsErrors` and passes all **60** tests on
> the .NET 10 SDK. The full pilot (publisher → agent → on-screen notification) has been verified
> end-to-end on a domain-joined workstation.
>
> **Install & usage:** see [`INSTRUCTIONS.md`](INSTRUCTIONS.md). Full modernization record:
> [`MODERNIZATION.md`](MODERNIZATION.md).

## Requirements

- **.NET 10 SDK** (the projects target `net10.0`). Change the `<TargetFramework>` in both
  `.csproj` files to `net8.0` if you need the previous LTS.
- Package restore uses **nuget.org**. A repo-local [`src/HearingsMessenger/nuget.config`](src/HearingsMessenger/nuget.config)
  configures the source, so no machine-wide NuGet setup is required.

## Build & test

```sh
dotnet build src/HearingsMessenger/HearingsMessenger.sln
dotnet test  src/HearingsMessenger/HearingsMessenger.sln
```

## What's in the box

### 1. The messenger hub (`HearingsMessenger`)

An in-process event aggregator: publishers and subscribers are decoupled through
`ITinyMessengerHub`. Supports all 8 original `Subscribe` overloads (with/without filters,
strong/weak references), typed and cancellable messages, message proxies, a pluggable
`ISubscriberErrorHandler`, and both synchronous `Publish` and awaitable `PublishAsync`
(with `CancellationToken`) plus first-class async subscribers via `SubscribeAsync`.

### 2. The broadcast layer (`HearingsMessenger.Broadcast`)

Purpose-built for fire-and-forget notifications to workstation groups:

```
IBroadcastPublisher.PublishAsync(notification, machineGroup)
        │
        ├── (optional) local echo on ITinyMessengerHub   ← in-proc subscribers/audit
        └── fan-out to ALL registered IBroadcastTransport
                ├── HttpAgentBroadcastTransport   ← recommended, included
                ├── LoopbackBroadcastTransport    ← dev/test, included
                └── your gRPC / Service Bus / WinRM implementation
```

- **`BroadcastNotification`** — immutable payload (Id, Title, Body, Severity, timestamps,
  extensible `Metadata`), serializes cleanly with `System.Text.Json`.
- **`IBroadcastTransport`** — the extension point. Contract: one-way, best-effort; per-machine
  failures are logged, never thrown.
- **`BroadcastPublisher`** — fans out to every registered transport concurrently and never
  throws for delivery failures (honours cancellation).

> **Receiving agent:** [`src/HearingsMessenger/HearingsMessenger.Agent`](src/HearingsMessenger/HearingsMessenger.Agent)
> — an ASP.NET Core minimal API (Windows Service) that receives the POSTs with Negotiate auth,
> serves HTTPS from an AD CS server cert, and shows each notification **on-screen** to logged-in
> users. `HttpAgentBroadcastTransport` POSTs JSON to that endpoint; the contract is in that file's
> XML docs and in `MODERNIZATION.md` §2.

## Dependency injection

Only `Microsoft.Extensions.*` abstractions are referenced — no container or logging framework
is forced on consumers.

```csharp
builder.Services.AddTinyMessenger();                          // ITinyMessengerHub singleton
builder.Services.AddTinyMessengerBroadcast(o => o.DefaultPort = 7443);
// inject IBroadcastPublisher; register additional transports:
builder.Services.AddSingleton<IBroadcastTransport, MyGrpcTransport>();
```

## Project layout

```
src/HearingsMessenger/
  HearingsMessenger.sln
  nuget.config
  HearingsMessenger/                 ← the library (net10.0)
    Broadcast/                       ← one-way broadcast layer
    DependencyInjection/             ← AddTinyMessenger / AddTinyMessengerBroadcast
    *.cs                             ← hub, messages, subscriptions, tokens, error handler
  HearingsMessenger.Tests/           ← MSTest + Moq test suite (60 tests)
  HearingsMessenger.Agent/           ← workstation receiving agent (ASP.NET Core Windows Service)
  HearingsMessenger.SendTest/        ← publisher CLI for pilot testing (console)
```

> Public **type names** keep their `TinyMessenger*` / `TinyMessage*` prefixes on purpose —
> they are the API heritage of the upstream library, so its documentation and examples apply
> directly. Only the project, assembly, and namespaces are `HearingsMessenger`.

## License

Microsoft Public License (Ms-PL) — see [`licence.txt`](licence.txt). Copyright © Steven Robbins;
core event-aggregator code originates from [grumpydev/TinyMessenger](https://github.com/grumpydev/TinyMessenger).
