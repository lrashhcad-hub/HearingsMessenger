//===============================================================================
// TinyMessenger.Broadcast — one-way broadcast layer for enterprise workstations.
// See MODERNIZATION.md and licence.txt.
//===============================================================================

namespace HearingsMessenger.Broadcast;

/// <summary>
/// Severity of a broadcast notification. Receiving agents may use this to
/// choose presentation (toast style, duration, sound, etc.).
/// </summary>
public enum BroadcastSeverity
{
    /// <summary>Informational notice.</summary>
    Information = 0,

    /// <summary>Something users should pay attention to.</summary>
    Warning = 1,

    /// <summary>Urgent, action-relevant notice (e.g. imminent outage).</summary>
    Critical = 2,
}

/// <summary>
/// Immutable one-way notification payload. Serializes cleanly with System.Text.Json;
/// receiving agents should ignore unknown fields so the record can be extended with
/// <c>init</c> properties without breaking older agents. For ad-hoc data, use
/// <see cref="Metadata"/>.
/// </summary>
public sealed record BroadcastNotification
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);

    /// <summary>Unique identifier for this notification (agents may use it for de-duplication).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Short title, e.g. "Maintenance window tonight".</summary>
    public required string Title { get; init; }

    /// <summary>Body text of the notification.</summary>
    public required string Body { get; init; }

    /// <summary>Severity of the notification.</summary>
    public BroadcastSeverity Severity { get; init; } = BroadcastSeverity.Information;

    /// <summary>Logical sender, e.g. "IT Systems" or a service account name.</summary>
    public string? Sender { get; init; }

    /// <summary>When the notification was created (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional expiry; agents should not display the notification after this time.</summary>
    public DateTimeOffset? ExpiresUtc { get; init; }

    /// <summary>Extension point for ad-hoc key/value data. Agents ignore keys they don't understand.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = EmptyMetadata;
}

/// <summary>
/// A single target workstation. Use real AD host names (not IPs) so Kerberos
/// service tickets resolve correctly. <see cref="Port"/> overrides the
/// transport's default port for this machine only.
/// </summary>
/// <param name="HostName">Fully qualified or short AD host name.</param>
/// <param name="Port">Optional per-machine port override.</param>
public sealed record MachineTarget(string HostName, int? Port = null);

/// <summary>
/// A named collection of target machines. Materialize groups from AD OUs or
/// security groups in your directory-query layer, then hand the result here —
/// the broadcast layer deliberately knows nothing about AD itself.
/// </summary>
public sealed record MachineGroup
{
    /// <summary>Display name of the group, e.g. "Downtown-Appraisers" or "All-Workstations".</summary>
    public required string Name { get; init; }

    /// <summary>The machines in the group.</summary>
    public required IReadOnlyList<MachineTarget> Machines { get; init; }

    /// <summary>
    /// Convenience factory for building a group from plain host names.
    /// </summary>
    /// <param name="name">Display name of the group.</param>
    /// <param name="hostNames">Host names of the member machines.</param>
    public static MachineGroup FromHostNames(string name, IEnumerable<string> hostNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(hostNames);

        return new MachineGroup
        {
            Name = name,
            Machines = hostNames.Select(h => new MachineTarget(h)).ToList(),
        };
    }

    /// <summary>
    /// Convenience factory for building a group from plain host names.
    /// </summary>
    /// <param name="name">Display name of the group.</param>
    /// <param name="hostNames">Host names of the member machines.</param>
    public static MachineGroup FromHostNames(string name, params string[] hostNames)
        => FromHostNames(name, (IEnumerable<string>)hostNames);
}
