# HearingsMessenger.SendTest

A small console **publisher** for exercising the broadcast layer end-to-end — the sender
half of the pilot (Phase 5 of [`../../../docs/PILOT-TEST-PLAN.md`](../../../docs/PILOT-TEST-PLAN.md)).

It wires up `AddTinyMessengerBroadcast`, builds a `MachineGroup`, and calls
`IBroadcastPublisher.PublishAsync`. Logging is at **Debug** on purpose: the transport is
best-effort and logs-then-swallows every per-machine failure, so without Debug/Warning
logs a broadcast that reached zero machines looks just like success.

## Usage

```sh
# 1) Prove the publish path with NO network first:
dotnet run --project src/HearingsMessenger/HearingsMessenger.SendTest -- --loopback

# 2) Then target real domain-joined hosts (run as a domain user — Kerberos/Negotiate
#    uses the current process identity):
dotnet run --project src/HearingsMessenger/HearingsMessenger.SendTest -- \
    WS-01.hcad.local WS-02.hcad.local --severity Warning --title "Maintenance tonight"
```

| Option | Default | Notes |
| --- | --- | --- |
| `[hosts...]` / `--hosts a,b,c` | — | Target FQDNs (**names, not IPs** — Kerberos) |
| `--loopback` | off | In-memory transport, no network — **run this first** |
| `--title` / `--body` | pilot defaults | Notification text |
| `--severity` | `Information` | `Information` \| `Warning` \| `Critical` |
| `--sender` | `IT Systems` | Logical sender label |
| `--port` | `7443` | Agent port (`BroadcastOptions.DefaultPort`) |
| `--timeout` | `10` | Per-machine send timeout (seconds) |

`--loopback` also prints what the in-memory transport recorded and shows the in-process
**local echo** firing, so you can confirm the publish path, group building, and audit hook
independently of any network or agent.
