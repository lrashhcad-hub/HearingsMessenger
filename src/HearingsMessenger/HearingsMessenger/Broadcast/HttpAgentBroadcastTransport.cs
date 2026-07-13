//===============================================================================
// TinyMessenger.Broadcast — one-way broadcast layer for enterprise workstations.
// See MODERNIZATION.md and licence.txt.
//===============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HearingsMessenger.Broadcast;

/// <summary>
/// Recommended transport for domain-joined Windows 11 workstations: POSTs the
/// JSON-serialized <see cref="BroadcastNotification"/> over HTTPS to a small agent
/// on each machine, authenticating with Windows integrated auth
/// (Kerberos/Negotiate via <see cref="CredentialCache.DefaultCredentials"/>).
///
/// Expected agent endpoint: an ASP.NET Core minimal API running as a Windows
/// Service, listening on <c>https://+:{port}{path}</c> (defaults:
/// <c>https://+:7443/api/notifications</c>) with Negotiate authentication, which
/// shows a toast (Windows App SDK) or republishes on the machine's local hub.
///
/// Deployment notes:
/// - Use real AD host names (not IPs) so Kerberos tickets resolve.
/// - Issue agent TLS certificates via AD CS auto-enrollment.
/// - Open the port via GPO/Intune firewall policy.
/// - <c>setspn -S HTTP/&lt;host&gt; &lt;account&gt;</c> if the agent runs under a custom account.
/// - Entra-joined-only machines: use client-certificate auth instead of Negotiate.
/// </summary>
public sealed class HttpAgentBroadcastTransport : IBroadcastTransport, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly BroadcastOptions _options;
    private readonly ILogger<HttpAgentBroadcastTransport> _logger;

    /// <inheritdoc />
    public string Name => "HttpAgent";

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpAgentBroadcastTransport"/> class.
    /// </summary>
    /// <param name="options">Broadcast options; defaults are used when null.</param>
    /// <param name="logger">Optional logger; a no-op logger is used when null.</param>
    /// <param name="httpClient">
    /// Optional pre-configured <see cref="HttpClient"/> (e.g. from IHttpClientFactory).
    /// When null, the transport creates and owns a client configured for Windows
    /// integrated authentication with the current process identity.
    /// </param>
    public HttpAgentBroadcastTransport(
        IOptions<BroadcastOptions>? options = null,
        ILogger<HttpAgentBroadcastTransport>? logger = null,
        HttpClient? httpClient = null)
    {
        _options = options?.Value ?? new BroadcastOptions();
        _logger = logger ?? NullLogger<HttpAgentBroadcastTransport>.Instance;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                // Kerberos/Negotiate with the identity of the publishing process.
                Credentials = CredentialCache.DefaultCredentials,
                PreAuthenticate = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };

            _httpClient = new HttpClient(handler, disposeHandler: true);
            _ownsHttpClient = true;
        }

        // Per-machine timeout is enforced via linked cancellation in SendToMachineAsync;
        // keep the client-level timeout out of the way.
        if (_ownsHttpClient)
        {
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(group);

        if (group.Machines.Count == 0)
        {
            _logger.LogWarning("Broadcast {NotificationId}: machine group '{Group}' is empty; nothing to send.", notification.Id, group.Name);
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrentSends),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(group.Machines, parallelOptions, async (machine, ct) =>
        {
            await SendToMachineAsync(notification, machine, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task SendToMachineAsync(BroadcastNotification notification, MachineTarget machine, CancellationToken cancellationToken)
    {
        var port = machine.Port ?? _options.DefaultPort;
        var uri = new UriBuilder(Uri.UriSchemeHttps, machine.HostName, port, _options.AgentPath).Uri;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.SendTimeout);

            using var response = await _httpClient
                .PostAsJsonAsync(uri, notification, SerializerOptions, timeoutCts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Broadcast {NotificationId} delivered to {Machine}.", notification.Id, machine.HostName);
            }
            else
            {
                _logger.LogWarning(
                    "Broadcast {NotificationId} to {Machine} returned HTTP {StatusCode}.",
                    notification.Id, machine.HostName, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Whole-broadcast cancellation: let it propagate.
            throw;
        }
        catch (Exception exception)
        {
            // Per-machine timeout, DNS failure, connection refusal, TLS/auth failure, etc.
            // One-way, best-effort contract: log and continue with the other machines.
            _logger.LogWarning(
                exception,
                "Broadcast {NotificationId} failed for machine {Machine} ({Uri}).",
                notification.Id, machine.HostName, uri);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
