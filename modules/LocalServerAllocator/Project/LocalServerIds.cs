using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalServerAllocatorModule;

/// <summary>
/// Maps a match to the local server it should be hosted on. Clients put their server's id in the matchmaker
/// player properties under <see cref="PlayerPropertyKey"/>; that server creates its control session as
/// <see cref="ToControlSessionId"/> of the same id.
/// </summary>
public static partial class LocalServerIds
{
    /// <summary>Player property (ticket custom data) key carrying the local server id.</summary>
    public const string PlayerPropertyKey = "localServerId";

    /// <summary>Prefix of the control session id a local server creates.</summary>
    public const string ControlSessionPrefix = "local-";

    /// <summary>The control session id the local server for <paramref name="localServerId"/> creates.</summary>
    public static string ToControlSessionId(string localServerId) => ControlSessionPrefix + localServerId;

    /// <summary>Reads the local server id shared by every player in a match.</summary>
    /// <param name="matchProperties">The allocate request's <c>MatchmakingResults.MatchProperties</c>.</param>
    /// <returns>
    /// The local server id, or <c>null</c> when a player lacks a valid one or the players disagree.
    /// </returns>
    public static string? FromMatchProperties(IReadOnlyDictionary<string, object>? matchProperties)
    {
        var players = matchProperties?
            .FirstOrDefault(p => string.Equals(p.Key, "players", StringComparison.OrdinalIgnoreCase)).Value;
        if (players is null)
            return null;

        using var document = JsonDocument.Parse(ToJson(players));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        string? localServerId = null;
        foreach (var player in document.RootElement.EnumerateArray())
        {
            var playerLocalServerId = ReadLocalServerId(player);
            if (playerLocalServerId is null || (localServerId is not null && localServerId != playerLocalServerId))
                return null;
            localServerId = playerLocalServerId;
        }

        return localServerId;
    }

    private static string? ReadLocalServerId(JsonElement player)
    {
        if (player.ValueKind != JsonValueKind.Object || !TryGetProperty(player, "customData", out var customData))
            return null;

        // Custom data may arrive as an object or as a JSON-encoded string.
        if (customData.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var inner = JsonDocument.Parse(customData.GetString()!);
                return ReadLocalServerIdFromCustomData(inner.RootElement);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return ReadLocalServerIdFromCustomData(customData);
    }

    private static string? ReadLocalServerIdFromCustomData(JsonElement customData)
    {
        if (customData.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(customData, PlayerPropertyKey, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;

        var localServerId = value.GetString()!;
        return ValidLocalServerId().IsMatch(localServerId) ? localServerId : null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    // The Cloud Code runtime may hand nested values over as Newtonsoft JTokens, System.Text.Json elements or
    // plain CLR objects; normalise to JSON text so the parsing above sees one shape.
    private static string ToJson(object value) => value switch
    {
        JsonElement element => element.GetRawText(),
        string text => text,
        _ when value.GetType().Namespace == "Newtonsoft.Json.Linq" => value.ToString()!,
        _ => JsonSerializer.Serialize(value)
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex ValidLocalServerId();
}
