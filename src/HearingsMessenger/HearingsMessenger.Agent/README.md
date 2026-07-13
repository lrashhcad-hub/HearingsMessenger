# HearingsMessenger.Agent

The **workstation receiving agent** for the broadcast layer — the counterpart to
`HttpAgentBroadcastTransport`. It's an ASP.NET Core minimal API, hosted as a Windows
Service, that accepts one-way `BroadcastNotification` POSTs and surfaces them locally.

> **Status: pilot scaffold — log-only.** It authenticates the caller, deserializes the
> notification (reusing the library's `BroadcastNotification`, so the wire contract can't
> drift), and logs it. Showing a Windows toast is Phase 6.4 of
> [`../../../docs/PILOT-TEST-PLAN.md`](../../../docs/PILOT-TEST-PLAN.md).

## Endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/notifications` | Negotiate (required) | Receives a broadcast; returns `200 OK` |
| `GET` | `/health` | Anonymous | Reachability probe |

Listens on **`https://+:7443`** by default (matches the transport's `DefaultPort`).

## Configuration (`appsettings.json` / env / user-secrets)

| Key | Default | Notes |
| --- | --- | --- |
| `Agent:Port` | `7443` | Listen port |
| `Agent:CertPath` | *(empty)* | PFX whose **SAN = this host's FQDN**. Empty ⇒ ASP.NET **dev cert** (local testing only) |
| `Agent:CertPassword` | *(empty)* | Supply via user-secrets/env in production, **not** this file |

## Run locally (dev)

```sh
dotnet dev-certs https --trust           # once, on the dev box
dotnet run --project src/HearingsMessenger/HearingsMessenger.Agent
# smoke test:
curl -k https://localhost:7443/health
```

## Install as a Windows Service (pilot)

```powershell
dotnet publish src/HearingsMessenger/HearingsMessenger.Agent -c Release
New-Service -Name HearingsAgent -BinaryPathName "<publish>\HearingsMessenger.Agent.exe" -StartupType Automatic
Start-Service HearingsAgent
```

Certificate binding, SPN (only for custom service accounts), and firewall rules are covered
in [`../../../docs/PILOT-TEST-PLAN.md`](../../../docs/PILOT-TEST-PLAN.md) (Phases 2–4).
