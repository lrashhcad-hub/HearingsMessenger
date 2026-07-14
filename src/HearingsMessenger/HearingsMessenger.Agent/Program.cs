//===============================================================================
// HearingsMessenger.Agent — workstation receiving agent for the broadcast layer.
//
// Listens for one-way BroadcastNotification POSTs from HttpAgentBroadcastTransport
// and surfaces them locally. This is the "receiving agent" referenced by
// HttpAgentBroadcastTransport and docs/PILOT-TEST-PLAN.md.
//
// Pilot scaffold: log-only. Phase 6.4 of the pilot plan adds a Windows toast.
//===============================================================================

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using HearingsMessenger.Broadcast;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows Service when installed; still runs as a plain console app for local testing.
builder.Host.UseWindowsService();

// --- Kestrel: HTTPS on the agent port (default 7443) ---
// Certificate resolution order: store thumbprint (preferred for AD CS-issued machine
// certs) -> PFX file -> ASP.NET dev cert (local testing only).
var port = builder.Configuration.GetValue("Agent:Port", 7443);
var certThumbprint = builder.Configuration["Agent:CertThumbprint"];
var certPath = builder.Configuration["Agent:CertPath"];
var certPassword = builder.Configuration["Agent:CertPassword"];

// Show a visible on-screen message to logged-in users when a broadcast arrives (default on).
var showPopup = builder.Configuration.GetValue("Agent:ShowPopup", true);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port, listen =>
    {
        if (!string.IsNullOrWhiteSpace(certThumbprint))
        {
            // Preferred: an AD CS / internal-CA server certificate already in the machine
            // store (LocalMachine\My). Trusted domain-wide, no private key exported to disk.
            // The cert MUST have a Server Authentication EKU and a SAN matching the host FQDN.
            listen.UseHttps(LoadCertificateByThumbprint(certThumbprint));
        }
        else if (!string.IsNullOrWhiteSpace(certPath))
        {
            // A PFX file on disk. The certificate's SAN MUST match the host FQDN.
            listen.UseHttps(certPath, certPassword);
        }
        else
        {
            // Local testing only: the ASP.NET Core developer certificate.
            // (`dotnet dev-certs https --trust` on the dev box.)
            listen.UseHttps();
        }
    });
});

static X509Certificate2 LoadCertificateByThumbprint(string thumbprint)
{
    var normalized = thumbprint.Replace(" ", string.Empty).Trim();
    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
    store.Open(OpenFlags.ReadOnly);
    var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);
    if (matches.Count == 0)
    {
        throw new InvalidOperationException(
            $"No certificate with thumbprint '{normalized}' found in LocalMachine\\My.");
    }

    return matches[0];
}

// Deliver a visible message to interactive sessions. A Session-0 service can't draw UI in
// a user's desktop directly, so we use Windows' built-in msg.exe (Terminal Services), which
// shows a message box from Session 0 to every logged-in session. Best-effort and non-blocking.
static void NotifyLoggedInUsers(BroadcastNotification notification, ILogger logger)
{
    var body = string.IsNullOrWhiteSpace(notification.Sender)
        ? notification.Body
        : $"{notification.Body}\n\n- {notification.Sender}";
    var text = $"[{notification.Severity}] {notification.Title}\n\n{body}";

    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "msg.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("*");          // every interactive session on this machine
        psi.ArgumentList.Add("/TIME:120");  // auto-dismiss after 120s if the user ignores it
        psi.ArgumentList.Add(text);

        // Fire-and-forget: don't wait, keep the HTTP response fast. Disposing the wrapper
        // does not terminate the launched msg.exe.
        Process.Start(psi)?.Dispose();
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Broadcast {Id}: failed to display on-screen message via msg.exe.", notification.Id);
    }
}

// --- Windows integrated authentication (Kerberos/Negotiate) ---
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

// Require an authenticated caller for every endpoint by default; /health opts out below.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// --- JSON: match the sender's contract exactly (System.Text.Json web defaults +
//     string-valued enums, as produced by HttpAgentBroadcastTransport). ---
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Unauthenticated reachability probe (handy for smoke tests / monitoring).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

// The one-way notification sink. Keep it fast and side-effect-light: the sender's
// transport is best-effort and only cares that we return a 2xx quickly.
app.MapPost("/api/notifications", (
        BroadcastNotification notification,
        HttpContext http,
        ILogger<Program> logger) =>
    {
        var caller = http.User.Identity?.Name ?? "(unauthenticated)";
        logger.LogInformation(
            "Broadcast {Id} from {Caller}: [{Severity}] {Title} — {Body} (sender: {Sender})",
            notification.Id, caller, notification.Severity, notification.Title,
            notification.Body, notification.Sender ?? "(none)");

        if (showPopup)
        {
            NotifyLoggedInUsers(notification, logger);
        }

        return Results.Ok();
    })
    .RequireAuthorization();

app.Run();
