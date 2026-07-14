# HearingsMessenger — Installation & Usage

End-to-end guide for building the solution, deploying the workstation **agent**, and sending
broadcast notifications with the **SendTest** publisher. For the design/architecture see
[`README.md`](README.md); for the pilot rollout runbook see [`docs/PILOT-TEST-PLAN.md`](docs/PILOT-TEST-PLAN.md).

The system has three parts:

| Component | Project | Role |
| --- | --- | --- |
| **Library** | `HearingsMessenger` | The event-aggregator hub + one-way broadcast layer (the reusable NuGet-style component). |
| **Agent** | `HearingsMessenger.Agent` | Windows Service on each workstation that receives broadcasts and shows them on-screen. |
| **Publisher** | `HearingsMessenger.SendTest` | Console tool that sends a broadcast to one or more agents. |

A broadcast is a one-way HTTPS `POST` of a JSON `BroadcastNotification` from the publisher to
each agent at `https://<host>:7443/api/notifications`, authenticated with Windows integrated
auth (Kerberos/Negotiate).

---

## 1. Prerequisites

- **Build machine:** .NET **10 SDK** (`dotnet --list-sdks` shows `10.x`). Download:
  <https://dotnet.microsoft.com/download/dotnet/10.0>.
- **NuGet:** restore uses nuget.org via the repo-local `src/HearingsMessenger/nuget.config` —
  no machine-wide NuGet config required.
- **Workstations (agents):** domain-joined Windows. No .NET runtime needed if you deploy the
  agent **self-contained** (recommended — it bundles the runtime).
- **Certificate:** each agent needs a server-auth TLS cert whose SAN matches the host FQDN.
  An **AD CS**-issued machine cert (trusted domain-wide) is preferred; a self-signed cert works
  for a quick pilot but must be trusted on the publisher.

---

## 2. Build & test

```powershell
dotnet build src/HearingsMessenger/HearingsMessenger.sln
dotnet test  src/HearingsMessenger/HearingsMessenger.sln
```

Both run warning-clean (the projects enable `TreatWarningsAsErrors`); the test suite is 60 tests.

---

## 3. Deploy the agent to a workstation

Steps below use PowerShell and assume **local admin** on the target. Replace `WS-01.hcad.local`
with the target FQDN.

### 3.1 Publish self-contained

```powershell
dotnet publish src/HearingsMessenger/HearingsMessenger.Agent -c Release -r win-x64 --self-contained true -o .\publish\agent
```

> If `dotnet publish` hits an intermittent `MSB3030` on `runtimeconfig.json`, the equivalent
> self-contained **build output** at
> `src/HearingsMessenger/HearingsMessenger.Agent/bin/Release/net10.0/win-x64` is complete and
> can be deployed directly.

### 3.2 Copy to the workstation

```powershell
Copy-Item -Path .\publish\agent\* -Destination "\\WS-01.hcad.local\C$\HearingsAgent" -Recurse -Force
```

### 3.3 Choose a certificate

**Preferred — an existing AD CS machine cert.** Find one with a Server Authentication EKU and a
SAN matching the host:

```powershell
Invoke-Command -ComputerName WS-01.hcad.local -ScriptBlock {
  Get-ChildItem Cert:\LocalMachine\My |
    Where-Object { $_.EnhancedKeyUsageList.FriendlyName -contains 'Server Authentication' -and $_.HasPrivateKey } |
    Select-Object Subject, Thumbprint, @{n='SAN';e={($_.DnsNameList.Unicode) -join ','}}, NotAfter
}
```

Note the **thumbprint** — you'll put it in the agent config. (No cert? Enroll one from your CA,
or generate a self-signed one: `New-SelfSignedCertificate -DnsName WS-01.hcad.local -CertStoreLocation Cert:\LocalMachine\My -Type SSLServerAuthentication`, then trust its public cert on the publisher.)

### 3.4 Write the agent configuration

Create/overwrite `C:\HearingsAgent\appsettings.json` on the workstation. Point it at the cert
thumbprint from 3.3:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "Agent": {
    "Port": 7443,
    "ShowPopup": true,
    "CertThumbprint": "BCE75CE3...THUMBPRINT..."
  }
}
```

### 3.5 Install the service, open the firewall, start

```powershell
Invoke-Command -ComputerName WS-01.hcad.local -ScriptBlock {
  New-Service -Name HearingsAgent -BinaryPathName 'C:\HearingsAgent\HearingsMessenger.Agent.exe' `
              -DisplayName 'HearingsMessenger Broadcast Agent' -StartupType Automatic
  New-NetFirewallRule -DisplayName 'HearingsAgent 7443' -Direction Inbound -Protocol TCP -LocalPort 7443 -Action Allow
  Start-Service HearingsAgent
}
```

### 3.6 Verify the agent is up

```powershell
# Anonymous health endpoint (validates the cert too, if issued by a trusted CA):
Invoke-WebRequest https://WS-01.hcad.local:7443/health -UseBasicParsing | Select-Object StatusCode  # -> 200
```

---

## 4. Agent configuration reference (`appsettings.json` → `Agent`)

| Key | Default | Meaning |
| --- | --- | --- |
| `Port` | `7443` | HTTPS listen port. Must match the publisher's `--port`. |
| `ShowPopup` | `true` | Show each received broadcast on-screen (via `msg.exe`) to logged-in sessions. `false` = log only. |
| `CertThumbprint` | *(empty)* | **Preferred.** Thumbprint of a server-auth cert in `LocalMachine\My`. Trusted domain-wide if AD CS-issued; no key on disk. Takes precedence over `CertPath`. |
| `CertPath` | *(empty)* | Fallback PFX path (SAN must match host FQDN). Used only if `CertThumbprint` is empty. Empty = ASP.NET dev cert (local testing only). |
| `CertPassword` | *(empty)* | PFX password. Prefer user-secrets/env over this file in production. |

Cert resolution order: **`CertThumbprint` → `CertPath` → dev cert.**

### Endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/notifications` | Negotiate (required) | Receive a broadcast; returns `200 OK`. |
| `GET` | `/health` | Anonymous | Reachability/health probe. |

### On-screen display note

The agent runs as a Windows Service in **Session 0**, which can't draw a modern Action Center
toast directly. It uses Windows' built-in **`msg.exe`** to show a message box from Session 0 to
every interactive session — dependency-free and reliable. A popup only appears if a user is
logged in (console or RDP) at send time. A true Action Center toast would require a helper
launched into the user session via `CreateProcessAsUser` (future enhancement).

---

## 5. Send a broadcast with `HearingsMessenger.SendTest`

Run it as a **domain user** — the publisher authenticates to agents with the current process
identity (Kerberos/Negotiate). Logging is at **Debug** so per-machine results are visible:
successful delivery logs `delivered to <host>` (agent returned 2xx); failures log a `Warning`
with the reason (the transport is best-effort and never throws).

```powershell
# 1) Prove the publish path with NO network first:
dotnet run --project src/HearingsMessenger/HearingsMessenger.SendTest -- --loopback

# 2) Send to real agents:
dotnet run --project src/HearingsMessenger/HearingsMessenger.SendTest -- `
    WS-01.hcad.local WS-02.hcad.local `
    --title "Maintenance tonight" --body "Systems down 6-7 PM." --severity Warning --sender "IT Systems"
```

(You can also run the built binary directly:
`HearingsMessenger.SendTest.exe WS-01.hcad.local --title "..."`.)

### 5.1 All options

| Option | Default | Description |
| --- | --- | --- |
| `[hosts...]` | — | One or more target FQDNs, space-separated. **Use names, not IPs** (Kerberos). |
| `--hosts a,b,c` | — | Comma-separated FQDNs (merged with any positional hosts). |
| `--loopback` | off | Use the in-memory transport (no network). **Run this first** to validate the publish path; ignores `--port`/`--timeout`. |
| `--title <text>` | `Pilot test` | Notification title. |
| `--body <text>` | *(a default sentence)* | Notification body. |
| `--severity <s>` | `Information` | `Information` \| `Warning` \| `Critical` (case-insensitive; unknown values are ignored). |
| `--sender <text>` | `IT Systems` | Logical sender label. |
| `--port <n>` | `7443` | Agent port (`BroadcastOptions.DefaultPort`). Must match the agent's `Agent:Port`. |
| `--timeout <secs>` | `10` | Per-machine send timeout. Each host gets its own timeout; a slow host doesn't hold up the others. |
| `-h`, `--help` | — | Print usage and exit. |

### 5.2 What success looks like

```
dbug: ...HttpAgentBroadcastTransport ... delivered to WS-01.hcad.local.
info: ...BroadcastPublisher ... sent via HttpAgent to group 'Pilot' (1 machines).
```

If `ShowPopup` is on and a user is logged into the target, a message box appears there.

---

## 6. Using the library in your own app

Only `Microsoft.Extensions.*` abstractions are referenced — no container/logging framework is forced.

```csharp
builder.Services.AddTinyMessenger();                          // ITinyMessengerHub singleton
builder.Services.AddTinyMessengerBroadcast(o => o.DefaultPort = 7443);
// inject IBroadcastPublisher; register additional transports as needed:
builder.Services.AddSingleton<IBroadcastTransport, MyGrpcTransport>();

// publish:
var group = MachineGroup.FromHostNames("Pilot", "WS-01.hcad.local", "WS-02.hcad.local");
await publisher.PublishAsync(new BroadcastNotification { Title = "…", Body = "…" }, group);
```

---

## 7. Troubleshooting

| Symptom (in publisher `Warning` logs) | Likely cause | Fix |
| --- | --- | --- |
| `UntrustedRoot` / TLS errors | Agent cert not trusted by the publisher, or SAN mismatch | Use an AD CS cert (trusted domain-wide) or trust the self-signed cert; ensure SAN = host FQDN. |
| `401 Unauthorized` | Kerberos/SPN, or publisher not running as a domain user | Run publisher as a domain user; use FQDN not IP. |
| Connection refused | Service not running / firewall / wrong port | Start `HearingsAgent`; open TCP 7443; match `--port`. |
| Timeout | Host unreachable / slow | Check network/DNS; raise `--timeout`. |
| Publisher "succeeds" but nothing arrives | No logger, or agent returned non-2xx | Watch the Debug/Warning logs (SendTest enables them). |
| Delivered but no popup | No interactive session, or `ShowPopup=false` | Log in to the target; set `Agent:ShowPopup=true`. |

---

## 8. Uninstall the agent

```powershell
Invoke-Command -ComputerName WS-01.hcad.local -ScriptBlock {
  Stop-Service HearingsAgent -Force -ErrorAction SilentlyContinue
  & sc.exe delete HearingsAgent
  Get-NetFirewallRule -DisplayName 'HearingsAgent 7443' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
  Remove-Item 'C:\HearingsAgent' -Recurse -Force -ErrorAction SilentlyContinue
}
```

(Also remove any self-signed cert you created from `Cert:\LocalMachine\My`; AD CS-issued certs
can be left in place.)
