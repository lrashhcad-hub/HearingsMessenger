//===============================================================================
// HearingsMessenger.SendTest — publisher CLI for exercising the broadcast layer.
//
// Pilot Phase 5: build a MachineGroup, publish a BroadcastNotification, and — crucially —
// log at Debug so the best-effort transport's per-machine results (which are otherwise
// logged-and-swallowed, never thrown) are actually visible. Use --loopback first to prove
// the publish path with no network (Phase 6 step 1), then target real hosts.
//===============================================================================

using HearingsMessenger;
using HearingsMessenger.Broadcast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var opts = ParseArgs(args);

if (opts.ShowHelp)
{
    PrintUsage();
    return 0;
}

if (!opts.Loopback && opts.Hosts.Count == 0)
{
    Console.Error.WriteLine("No target hosts given. Pass one or more hostnames, or --loopback. Use --help for details.");
    return 1;
}

// --- Composition root ---
var services = new ServiceCollection();

// Debug-level console logging is the whole point of a test harness here: the transport
// logs delivery at Debug and every per-machine failure at Warning, then swallows it.
services.AddLogging(builder => builder
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
    .SetMinimumLevel(LogLevel.Debug));

LoopbackBroadcastTransport? loopbackTransport = null;

if (opts.Loopback)
{
    // Loopback only: register the in-memory transport instead of the HTTP one so nothing
    // touches the network. Keep the instance so we can print what it recorded.
    loopbackTransport = new LoopbackBroadcastTransport();
    services.AddTinyMessenger();           // local hub, for the echo observer below
    services.AddOptions();
    services.AddSingleton<IBroadcastTransport>(loopbackTransport);
    services.AddSingleton<IBroadcastPublisher, BroadcastPublisher>();
}
else
{
    // Real fan-out via HttpAgentBroadcastTransport (Kerberos/Negotiate as the current user).
    services.AddTinyMessengerBroadcast(o =>
    {
        o.DefaultPort = opts.Port;
        o.SendTimeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    });
}

using var provider = services.BuildServiceProvider();

// Observe the in-process "local echo" so you can see the audit hook fire on every publish.
var hub = provider.GetService<ITinyMessengerHub>();
hub?.Subscribe<BroadcastNotificationMessage>(m =>
    Console.WriteLine($"  [local echo] '{m.Notification.Title}' -> group '{m.Group.Name}' ({m.Group.Machines.Count} machine(s))"));

var notification = new BroadcastNotification
{
    Title = opts.Title,
    Body = opts.Body,
    Severity = opts.Severity,
    Sender = opts.Sender,
};

var group = opts.Loopback
    ? MachineGroup.FromHostNames("Loopback", opts.Hosts.Count > 0 ? [.. opts.Hosts] : ["loopback-only"])
    : MachineGroup.FromHostNames("Pilot", [.. opts.Hosts]);

var publisher = provider.GetRequiredService<IBroadcastPublisher>();

Console.WriteLine(opts.Loopback
    ? $"Publishing '{notification.Title}' [{notification.Severity}] via LOOPBACK (no network) ..."
    : $"Publishing '{notification.Title}' [{notification.Severity}] to {group.Machines.Count} host(s) on port {opts.Port} ...");

await publisher.PublishAsync(notification, group);

if (loopbackTransport is not null)
{
    Console.WriteLine($"Loopback recorded {loopbackTransport.Sent.Count} send(s):");
    foreach (var record in loopbackTransport.Sent)
    {
        Console.WriteLine($"  - {record.SentUtc:HH:mm:ss} '{record.Notification.Title}' -> '{record.Group.Name}' ({record.Group.Machines.Count} machine(s))");
    }
}
else
{
    Console.WriteLine("Publish returned. Delivery is BEST-EFFORT: check the Debug/Warning logs");
    Console.WriteLine("above for per-machine results — failures are logged, never thrown.");
}

return 0;

// --------------------------------------------------------------------------------------

static Options ParseArgs(string[] args)
{
    var options = new Options();
    var hosts = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help" or "-h":
                options.ShowHelp = true;
                break;
            case "--loopback":
                options.Loopback = true;
                break;
            case "--title":
                options.Title = Next(args, ref i);
                break;
            case "--body":
                options.Body = Next(args, ref i);
                break;
            case "--sender":
                options.Sender = Next(args, ref i);
                break;
            case "--severity":
                if (Enum.TryParse<BroadcastSeverity>(Next(args, ref i), ignoreCase: true, out var severity))
                {
                    options.Severity = severity;
                }
                break;
            case "--port":
                if (int.TryParse(Next(args, ref i), out var port))
                {
                    options.Port = port;
                }
                break;
            case "--timeout":
                if (int.TryParse(Next(args, ref i), out var timeout))
                {
                    options.TimeoutSeconds = timeout;
                }
                break;
            case "--hosts":
                hosts.AddRange(Next(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            default:
                if (!args[i].StartsWith('-'))
                {
                    hosts.Add(args[i]);
                }
                break;
        }
    }

    options.Hosts = hosts;
    return options;
}

static string Next(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : string.Empty;

static void PrintUsage()
{
    Console.WriteLine(
        """
        HearingsMessenger.SendTest — publish a broadcast notification for testing.

        Usage:
          SendTest [hosts...] [options]
          SendTest --loopback [options]

        Targets:
          hosts...            One or more target FQDNs (space-separated), e.g. WS-01.hcad.local
          --hosts a,b,c       Comma-separated FQDNs (alternative to positional)
          --loopback          Use the in-memory transport (no network) — run this FIRST

        Notification:
          --title <text>      Default: "Pilot test"
          --body <text>       Default: a confirmation sentence
          --severity <s>      Information | Warning | Critical (default: Information)
          --sender <text>     Default: "IT Systems"

        Transport:
          --port <n>          Agent port (default: 7443)
          --timeout <secs>    Per-machine send timeout (default: 10)

          -h, --help          Show this help

        Examples:
          SendTest --loopback
          SendTest WS-01.hcad.local WS-02.hcad.local --severity Warning
          SendTest --hosts WS-01.hcad.local,WS-02.hcad.local --title "Maintenance tonight"
        """);
}

sealed class Options
{
    public bool ShowHelp { get; set; }
    public bool Loopback { get; set; }
    public List<string> Hosts { get; set; } = [];
    public string Title { get; set; } = "Pilot test";
    public string Body { get; set; } = "If you can read this, the broadcast path works.";
    public BroadcastSeverity Severity { get; set; } = BroadcastSeverity.Information;
    public string? Sender { get; set; } = "IT Systems";
    public int Port { get; set; } = 7443;
    public int TimeoutSeconds { get; set; } = 10;
}
