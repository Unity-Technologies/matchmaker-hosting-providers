using System;
using System.Collections.Generic;

namespace GameyeAllocatorModule;

/// <summary>
/// Gameye API environment. Determines which API endpoint the allocator targets.
/// </summary>
public enum GameyeEnvironment
{
    Sandbox,
    Production,
}

/// <summary>
/// Configuration for the Gameye allocator. Register an instance of this class
/// as a singleton in <see cref="ModuleConfig.Setup"/> to control allocator behavior.
/// </summary>
public class GameyeAllocatorConfig
{
    /// <summary>
    /// The Gameye API environment to use. Defaults to <see cref="GameyeEnvironment.Sandbox"/>.
    /// </summary>
    public GameyeEnvironment Environment { get; set; } = GameyeEnvironment.Sandbox;

    /// <summary>
    /// The application image name as registered in the Gameye Admin Panel.
    /// Must match exactly.
    /// </summary>
    public required string ImageName { get; set; }

    /// <summary>
    /// The default deployment region (e.g. "europe", "us-east-1").
    /// Used when the matched pool has no entry in <see cref="LocationByPool"/>.
    /// See https://www.gameye.com/docs/api-v2/available-locations/ for the full list.
    /// </summary>
    public string DefaultLocation { get; set; } = "europe";

    /// <summary>
    /// Maps Unity QoS region identifiers to Gameye location IDs.
    /// When Unity Matchmaker resolves a QoS region for the match, its value arrives in
    /// <c>MatchProperties["Region"]</c>. If that value is found here, the corresponding
    /// Gameye location is used. This is the preferred approach when Unity QoS is configured —
    /// no per-region pools are needed and the region decision is driven by real player latency.
    ///
    /// The key is whatever Unity puts in <c>MatchProperties["Region"]</c>: a human-readable
    /// name (e.g. <c>"eu-west"</c>) or a QoS region UUID.
    /// <example>
    /// <code>
    /// LocationByRegion = new Dictionary&lt;string, string&gt;
    /// {
    ///     { "eu-west",        "eu-west"        },
    ///     { "us-central",     "us-central"     },
    ///     { "asia-northeast", "asia-northeast" },
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public Dictionary<string, string> LocationByRegion { get; set; } = new();

    /// <summary>
    /// Maps Unity Matchmaker pool names to Gameye location IDs.
    /// Used when Unity QoS is not configured or <c>MatchProperties["region"]</c> has no entry
    /// in <see cref="LocationByRegion"/>. Use this alongside per-region pools in your
    /// Matchmaker queue configuration.
    /// <example>
    /// <code>
    /// LocationByPool = new Dictionary&lt;string, string&gt;
    /// {
    ///     { "eu-west-pool",    "eu-west"        },
    ///     { "us-central-pool", "us-central"     },
    ///     { "ap-ne-pool",      "asia-northeast" },
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public Dictionary<string, string> LocationByPool { get; set; } = new();

    /// <summary>
    /// The primary game server port. This must match the port exposed in your Dockerfile
    /// and configured in the Gameye Admin Panel. Used for the Unity Matchmaker
    /// <see cref="Unity.Services.CloudCode.Apis.Matchmaker.AssignmentData.IpPort"/> assignment.
    /// </summary>
    public int GamePort { get; set; } = 7777;

    /// <summary>
    /// Optional additional ports to include in allocation data (e.g. query port, RCON port).
    /// These are returned in <c>AllocationData</c> as <c>port_{name}</c> entries so
    /// game clients can access them alongside the primary port.
    /// Key: a descriptive name (e.g. "query", "rcon"). Value: the container port number.
    /// </summary>
    public Dictionary<string, int> AdditionalPorts { get; set; } = new();

    /// <summary>
    /// Optional Docker image tag / version. When set, Gameye starts a session using this
    /// specific image version instead of the highest-priority tag.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Returns the base API URL for the configured environment.
    /// </summary>
    public string ApiBaseUrl => Environment switch
    {
        GameyeEnvironment.Sandbox => "https://api.sandbox-gameye.gameye.net",
        GameyeEnvironment.Production => "https://api-production-gameye.gameye.net",
        _ => throw new ArgumentOutOfRangeException(nameof(Environment), Environment, "Unknown Gameye environment"),
    };
}
