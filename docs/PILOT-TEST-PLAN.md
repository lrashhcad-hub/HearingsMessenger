# HearingsMessenger — Broadcast Pilot Test Plan

Goal: prove the one-way broadcast path end-to-end against **a few domain-joined
Windows 11 workstations**, using the recommended `HttpAgentBroadcastTransport`.

> **Scope:** a *pilot* (2–3 machines), not a production rollout. Prefer per-machine
> manual setup here; convert to GPO/Intune/AD CS automation only once the pilot works.

> **✅ Pilot status (executed & verified):** both halves now exist in this repo
> (`HearingsMessenger.SendTest` publisher, `HearingsMessenger.Agent` receiver). The full path —
> publisher → HTTPS (AD CS cert) → Kerberos/NTLM auth → agent → **on-screen popup** — has been
> verified end-to-end on a domain-joined VM. This document remains the reusable runbook for
> standing up additional machines; step-by-step install/usage is in [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md).

---

## The two pieces you must have

The library is only the **sender-side** half. A broadcast is an HTTPS POST of a JSON
`BroadcastNotification` to `https://<host>:7443/api/notifications`. So you need:

1. **A publisher app** — provided: `HearingsMessenger.SendTest`. ✅
2. **A receiving agent on each workstation** that listens on that endpoint — provided:
   `HearingsMessenger.Agent`. ✅ (Originally the critical path; now built, deployed, and verified.)

### ⚠️ The #1 testing pitfall (from the transport's contract)

`HttpAgentBroadcastTransport` is *best-effort*: every per-machine failure (DNS, refused
connection, TLS, 401, timeout, non-2xx) is **logged and swallowed — never thrown.**
`PublishAsync` returns successfully even if **zero** machines received anything.

**Therefore: wire up an `ILogger` at `Debug`/`Warning` before you test, or you will have
no idea what happened.** Successful delivery logs at `Debug`; every failure logs at
`Warning`. No logger = flying blind.

---

## Phase 0 — Decisions & prerequisites

- [ ] Pick **2–3 pilot workstations**, all joined to the **same AD domain** as the machine
      that will run the publisher.
- [ ] Record their **real FQDNs** (e.g. `WS-APPR-01.hcad.local`). Use names, **never IPs** —
      Kerberos service tickets are keyed on the SPN/host name.
- [ ] Choose the **agent's service identity**:
      - `LocalSystem` (simplest for a pilot — authenticates as the computer account, whose
        `HOST/<fqdn>` SPN already exists, so no `setspn` needed), or
      - a dedicated service account / gMSA (more realistic; requires an SPN — see Phase 3).
- [ ] Choose the **auth model**: domain-joined → **Negotiate/Kerberos** (this plan).
      Entra-joined-only machines can't use Negotiate → use client-certificate auth instead.
- [ ] Confirm **TCP 7443** is free on the workstations (or pick another port and set it in
      `BroadcastOptions.DefaultPort` / per-machine `MachineTarget.Port`).

## Phase 1 — Build the receiving agent (the blocker)

> **A pilot scaffold already exists** at `src/HearingsMessenger/HearingsMessenger.Agent`
> (log-only, Negotiate auth, verified locally: `/health` → 200, unauthenticated POST → 401,
> authenticated POST of a real notification → 200). The steps below describe how it was built
> and what still needs doing (cert binding, the toast in Phase 6.4).

Create a new **ASP.NET Core minimal API** project (net10.0) hosted as a **Windows Service**.
It only needs to accept the POST, return 2xx fast, and surface the message.

- [ ] `dotnet new web -n HearingsMessenger.Agent` (separate solution/repo or a new project here).
- [ ] Add packages: `Microsoft.AspNetCore.Authentication.Negotiate`,
      `Microsoft.Extensions.Hosting.WindowsServices`.
- [ ] Match the **JSON contract** the transport sends (System.Text.Json *web* defaults:
      camelCase, enum-as-string). Minimal shape:

      ```json
      {
        "id": "GUID",
        "title": "…",
        "body": "…",
        "severity": "Information | Warning | Critical",
        "sender": "…",
        "createdUtc": "2026-07-13T20:00:00+00:00",
        "expiresUtc": null,
        "metadata": { }
      }
      ```

- [ ] Minimal agent (`Program.cs`) — start with **log-only**, add the toast in Phase 6:

      ```csharp
      using Microsoft.AspNetCore.Authentication.Negotiate;

      var builder = WebApplication.CreateBuilder(args);
      builder.Host.UseWindowsService();                       // run as a Windows Service
      builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(7443, o => o.UseHttps())); // cert: Phase 2
      builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
      builder.Services.AddAuthorization();

      var app = builder.Build();
      app.UseAuthentication();
      app.UseAuthorization();

      app.MapPost("/api/notifications", (Notification n, ILogger<Program> log) =>
      {
          log.LogInformation("Broadcast {Id}: [{Sev}] {Title} — {Body}", n.Id, n.Severity, n.Title, n.Body);
          // Phase 6: show a Windows toast here.
          return Results.Ok();
      }).RequireAuthorization();

      app.Run();

      record Notification(Guid Id, string Title, string Body, string Severity,
                          string? Sender, DateTimeOffset CreatedUtc,
                          DateTimeOffset? ExpiresUtc, Dictionary<string,string>? Metadata);
      ```

- [ ] Log to a file or the Windows **Event Log** so you can confirm receipt on each machine.

## Phase 2 — TLS certificates

- [ ] Give each agent host a **server-auth certificate** whose **SAN matches the FQDN** the
      publisher will use.
      - Pilot-fast: internal **AD CS** computer cert (auto-enrollment) or a cert issued from
        your domain CA. Avoid self-signed unless you'll also trust it on the sender.
- [ ] Bind the cert to Kestrel (by store+thumbprint via config, or a PFX path).
- [ ] Ensure the **publisher machine trusts the issuing CA** (domain root CA is usually
      already trusted on domain members). TLS trust failures otherwise show as `Warning`
      logs on the sender.

## Phase 3 — Deploy the agent to the pilot machines

- [ ] Publish: `dotnet publish -c Release` (framework-dependent is fine — .NET 10 runtime is
      present) and copy to each workstation.
- [ ] Install as a service, e.g. `New-Service -Name HearingsAgent -BinaryPathName "<path>\HearingsMessenger.Agent.exe" -StartupType Automatic` and `Start-Service HearingsAgent`.
- [ ] Run under the identity chosen in Phase 0.
- [ ] **SPN** (only if using a custom service account — skip for LocalSystem):
      `setspn -S HTTP/WS-APPR-01.hcad.local DOMAIN\svc-hearings-agent`
- [ ] Confirm the service is **listening**: `Test-NetConnection WS-APPR-01.hcad.local -Port 7443` from the sender.

## Phase 4 — Firewall / network

- [ ] Open **inbound TCP 7443** on each pilot machine. For the pilot, locally:
      `New-NetFirewallRule -DisplayName "Hearings Agent 7443" -Direction Inbound -Protocol TCP -LocalPort 7443 -Action Allow`
      (production: push via GPO/Intune).
- [ ] Confirm DNS resolves each FQDN from the sender.

## Phase 5 — Build the publisher test app

> **This app already exists** at `src/HearingsMessenger/HearingsMessenger.SendTest`
> (Debug logging on by default; `--loopback` mode verified, and an end-to-end run against
> the local agent confirmed the transport targets the right endpoint and logs+swallows a
> TLS failure — the pitfall above, live). See its README for usage.

- [ ] `dotnet new console -n HearingsMessenger.SendTest`, reference the library
      (project reference or the packed `.nupkg`).
- [ ] Wire DI + **logging** (remember the pitfall):

      ```csharp
      using HearingsMessenger.Broadcast;
      using Microsoft.Extensions.DependencyInjection;
      using Microsoft.Extensions.Logging;

      var services = new ServiceCollection();
      services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Debug)); // SEE failures
      services.AddTinyMessengerBroadcast(o => { o.DefaultPort = 7443; o.SendTimeout = TimeSpan.FromSeconds(10); });
      var sp = services.BuildServiceProvider();

      var publisher = sp.GetRequiredService<IBroadcastPublisher>();
      var group = MachineGroup.FromHostNames("Pilot",
          "WS-APPR-01.hcad.local", "WS-APPR-02.hcad.local");
      var note = new BroadcastNotification
      {
          Title = "Pilot test", Body = "If you can read this, broadcast works.",
          Severity = BroadcastSeverity.Information, Sender = "IT Systems",
      };
      await publisher.PublishAsync(note, group);
      ```

- [ ] **Run the publisher as a domain user** (interactive logon) — the transport uses
      `CredentialCache.DefaultCredentials`, i.e. the *publishing process's* identity, to get
      Kerberos tickets. Running as a non-domain / no-ticket identity → 401s.

## Phase 6 — Run & verify (build up gradually)

1. [ ] **Loopback first (no network):** register `LoopbackBroadcastTransport` instead of the
       HTTP one to confirm the publish path, group building, and local echo work in-proc.
2. [ ] **One real machine:** point the group at a single pilot host. Expect the publisher to
       log `Debug` "delivered" and the agent to log receipt (Event Log/file).
3. [ ] **The full pilot group:** add the other machines; confirm each agent logs receipt.
4. [ ] **Add the toast** to the agent (Windows App SDK / a toast library) and re-verify a
       notification actually appears to the logged-in user.
5. [ ] **Failure drill:** stop one agent and re-broadcast — confirm the publisher logs a
       `Warning` for that host and still succeeds for the others (best-effort contract).

## Phase 7 — Troubleshooting matrix

| Symptom (in sender `Warning` logs) | Likely cause | Fix |
| --- | --- | --- |
| `401 Unauthorized` | Kerberos/SPN, or sender not running as a domain user | Verify SPN; use FQDN not IP; run publisher as domain user |
| TLS / trust / SNI errors | Cert SAN mismatch or CA not trusted on sender | Reissue cert with correct SAN; trust the CA |
| Connection refused | Service not running / firewall / wrong port | Start service; open 7443; check `DefaultPort` |
| Timeout after ~10s | Host unreachable / slow | Check network; adjust `BroadcastOptions.SendTimeout` |
| Publisher "succeeds" but nothing arrives | **No logger configured** | Add `ILogger` at `Debug` — see the pitfall at top |

---

## Definition of done for the pilot

- A `BroadcastNotification` published from the sender reaches all pilot machines' agents.
- Each agent logs receipt (and, after Phase 6.4, shows a toast).
- Stopping one agent produces a `Warning` on the sender and does not break delivery to the rest.
- Kerberos/Negotiate auth succeeds with **no** credentials in the publisher code.
