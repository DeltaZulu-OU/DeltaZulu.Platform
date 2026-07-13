using System.Text.Json;

namespace DeltaZulu.Agent.Simulator;

public sealed record AgentIdentity(string AgentId, string AgentSecret, string Hostname);

/// <summary>
/// Persists the enrolled identity so restarts reuse the same agent. Plaintext
/// on disk is acceptable for this development-only tool.
/// </summary>
public static class AgentIdentityStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static AgentIdentity? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        return JsonSerializer.Deserialize<AgentIdentity>(File.ReadAllText(path), Options);
    }

    public static void Save(string path, AgentIdentity identity) =>
        File.WriteAllText(path, JsonSerializer.Serialize(identity, Options));
}
