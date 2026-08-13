using System.Globalization;
using DeltaZulu.Agent.Simulator;

// DeltaZulu agent simulator: exercises the real HTTPS pull loop against a
// running platform host — enroll once, then heartbeat / pull / ack forever.
//
// Usage:
//   dotnet run --project tools/DeltaZulu.Agent.Simulator -- \
//     --token dz-et-... [--base-url https://localhost:56196] [--hostname NAME] \
//     [--interval 30] [--state-file agent-identity.json] [--insecure]

var arguments = ParseArguments(args);
var baseUrl = new Uri(arguments.GetValueOrDefault("base-url", "https://localhost:56196"));
var bootstrapToken = arguments.GetValueOrDefault("token");
var hostname = arguments.GetValueOrDefault("hostname", Environment.MachineName.ToLowerInvariant());
var intervalSeconds = int.Parse(arguments.GetValueOrDefault("interval", "30"), CultureInfo.InvariantCulture);
var stateFile = arguments.GetValueOrDefault("state-file", "agent-identity.json");
var insecure = arguments.ContainsKey("insecure");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

using var client = new ControlPlaneClient(baseUrl, insecure);

var identity = AgentIdentityStore.Load(stateFile);
if (identity is null || !string.Equals(identity.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(bootstrapToken))
    {
        Console.Error.WriteLine("No stored identity found. Pass --token dz-et-... to enroll.");
        return 1;
    }

    Log($"Enrolling '{hostname}' at {baseUrl}...");
    var enrollment = await client.EnrollAsync(new EnrollRequest(
        bootstrapToken, hostname, OperatingSystem.IsWindows() ? "Windows" : "Linux",
        AgentVersion: "1.0.0-sim", Tags: ["simulator"]), cancellation.Token);

    identity = new AgentIdentity(enrollment.AgentId, enrollment.AgentSecret, hostname);
    AgentIdentityStore.Save(stateFile, identity);
    intervalSeconds = Math.Max(5, enrollment.HeartbeatIntervalSeconds);
    Log($"Enrolled as agent {enrollment.AgentId}; identity stored in {stateFile}.");
}
else
{
    Log($"Reusing stored identity {identity.AgentId} from {stateFile}.");
}

client.UseAgentSecret(identity.AgentSecret);

var health = new SyntheticHealth();
string? appliedBundleId = null;
string? appliedBundleHash = null;

Log($"Starting pull loop (every {intervalSeconds}s). Ctrl+C to stop.");
while (!cancellation.IsCancellationRequested)
{
    try
    {
        var (bufferPressure, queueDepth, dropped, forwardFailed, status) = health.Next();

        var heartbeat = await client.HeartbeatAsync(new HeartbeatRequest(
            "1.0.0-sim", appliedBundleId, appliedBundleHash, status,
            bufferPressure, queueDepth, dropped, forwardFailed,
            health.NextSources(DateTimeOffset.UtcNow)), cancellation.Token);

        Log($"Heartbeat ok (status={status}, buffer={bufferPressure:P0}); " +
            $"desired={Short(heartbeat.DesiredBundleId)} changed={heartbeat.PolicyChanged} " +
            $"commands={heartbeat.Commands?.Count ?? 0}");

        foreach (var command in heartbeat.Commands ?? [])
        {
            Log($"Executing command {command.Type} ({Short(command.CommandId)})...");
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation.Token);
            var result = command.Type == "CollectDiagnostics"
                ? """{"service":"running","diskFreeBytes":52428800000,"channels":["Security","Sysmon"]}"""
                : """{"ok":true}""";
            await client.PostCommandResultAsync(command.CommandId,
                new CommandResultRequest(true, result, null), cancellation.Token);
            Log($"Command {Short(command.CommandId)} completed.");
        }

        if (heartbeat.PolicyChanged && heartbeat.DesiredBundleId is not null)
        {
            var bundle = await client.GetBundleAsync(cancellation.Token);
            Log($"Pulled bundle {Short(bundle.BundleId)} (hash {bundle.ContentHash[..12]}, " +
                $"{bundle.Document.GetProperty("profiles").GetArrayLength()} profile(s)). Applying...");

            // A real agent would rewrite its local configuration here.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);

            await client.AckAsync(new AckRequest(bundle.BundleId, "Applied", null), cancellation.Token);
            appliedBundleId = bundle.BundleId;
            appliedBundleHash = bundle.ContentHash;
            Log($"Acked bundle {Short(bundle.BundleId)} as Applied.");
        }
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Log($"ERROR: {ex.Message}");
    }

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Log("Simulator stopped.");
return 0;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
            continue;

        var key = args[i][2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            parsed[key] = args[i + 1];
            i++;
        }
        else
        {
            parsed[key] = "true";
        }
    }
    return parsed;
}

static string Short(string? id) => id is null ? "none" : id[..Math.Min(8, id.Length)];

static void Log(string message) =>
    Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
