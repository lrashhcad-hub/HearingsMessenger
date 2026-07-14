# HearingsMessenger.Agent

The **workstation receiving agent** for the broadcast layer — the counterpart to
`HttpAgentBroadcastTransport`. It's an ASP.NET Core minimal API, hosted as a Windows
Service, that accepts one-way `BroadcastNotification` POSTs and surfaces them locally.

> **Status: pilot.** It authenticates the caller, deserializes the notification (reusing the
> library's `BroadcastNotification`, so the wire contract can't drift), logs it, and — unless
> `Agent:ShowPopup` is false — shows it **on-screen** to logged-in users.

## On-screen display

Because the agent runs as a Windows Service in **Session 0** (isolated from user desktops), it
can't draw a modern Action Center toast directly. It uses Windows' built-in **`msg.exe`** to
show a message box from Session 0 to every interactive session — dependency-free and reliable.
Toggle with `Agent:ShowPopup` (default `true`). A true Action Center toast would require a
helper process launched into the user session via `CreateProcessAsUser` (a later enhancement).

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
| `Agent:ShowPopup` | `true` | Show received broadcasts on-screen via `msg.exe`; `false` = log-only |
| `Agent:CertThumbprint` | *(empty)* | **Preferred.** Thumbprint of a server-auth cert in `LocalMachine\My` (e.g. AD CS-issued). Trusted domain-wide, no private key on disk. **Takes precedence** over `CertPath` |
| `Agent:CertPath` | *(empty)* | Fallback PFX whose **SAN = this host's FQDN**, used only when `CertThumbprint` is empty. Empty ⇒ ASP.NET **dev cert** (local testing only) |
| `Agent:CertPassword` | *(empty)* | PFX password; supply via user-secrets/env in production, **not** this file |

Certificate resolution order: **`CertThumbprint` → `CertPath` → dev cert**. The chosen cert must
have a **Server Authentication** EKU and a SAN matching the host FQDN the publisher targets.

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
