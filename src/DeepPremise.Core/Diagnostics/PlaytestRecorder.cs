using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeepPremise.Core.Diagnostics;

public sealed record StateChange(string[] Path, JsonNode? Value, bool Remove = false);

// Array-aware structural changes keep long sessions readable and substantially smaller than per-tick saves.
public static class StateDelta
{
    public static List<StateChange> Between(JsonNode? before, JsonNode? after)
    {
        var changes = new List<StateChange>();
        void Walk(JsonNode? left, JsonNode? right, string[] path)
        {
            if (JsonNode.DeepEquals(left, right)) return;
            if (left is JsonObject a && right is JsonObject b)
            {
                foreach (var key in a.Select(p => p.Key).Except(b.Select(p => p.Key))) changes.Add(new([.. path, key], null, true));
                foreach (var (key, value) in b)
                    if (!a.ContainsKey(key)) changes.Add(new([.. path, key], value?.DeepClone()));
                    else Walk(a[key], value, [.. path, key]);
            }
            else if (left is JsonArray x && right is JsonArray y)
            {
                for (var i = x.Count - 1; i >= y.Count; i--) changes.Add(new([.. path, i.ToString()], null, true));
                for (var i = 0; i < y.Count; i++)
                {
                    if (i >= x.Count) changes.Add(new([.. path, i.ToString()], y[i]?.DeepClone()));
                    else Walk(x[i], y[i], [.. path, i.ToString()]);
                }
            }
            else changes.Add(new(path, right?.DeepClone()));
        }
        Walk(before, after, []); return changes;
    }
    public static JsonNode Apply(JsonNode state, IEnumerable<StateChange> changes)
    {
        var result = state.DeepClone();
        foreach (var change in changes)
        {
            if (change.Path.Length == 0) { result = change.Value!.DeepClone(); continue; }
            var parent = result;
            foreach (var part in change.Path.SkipLast(1)) parent = parent is JsonArray a ? a[int.Parse(part)]! : parent[part]!;
            var key = change.Path[^1];
            if (parent is JsonArray array)
            {
                var index = int.Parse(key);
                if (change.Remove) array.RemoveAt(index);
                else if (index == array.Count) array.Add(change.Value?.DeepClone());
                else array[index] = change.Value?.DeepClone();
            }
            else if (change.Remove) parent.AsObject().Remove(key);
            else parent[key] = change.Value?.DeepClone();
        }
        return result;
    }
}

// First-playtest consent is explicit. All records stay on disk; credentials are never accepted by this API.
public sealed class PlaytestRecorder : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly StreamWriter writer;
    private readonly IRecordedWorld world;
    private JsonNode previous;
    private long sequence;
    private int checkpoints;
    private bool disposed;
    public string DirectoryPath { get; }
    public string SessionId { get; }
    public string Failure { get; private set; } = "";
    public bool Healthy => Failure == "" && !disposed;
    public long LastSequence => sequence;
    public PlaytestRecorder(string root, IRecordedWorld runner, string version, string language)
    {
        world = runner;
        SessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        DirectoryPath = Path.Combine(root, SessionId);
        Directory.CreateDirectory(DirectoryPath);
        var initial = world.SaveJson(); previous = JsonNode.Parse(initial)!;
        File.WriteAllText(Path.Combine(DirectoryPath, "initial-state.json"), initial);
        File.WriteAllText(Path.Combine(DirectoryPath, "manifest.json"), JsonSerializer.Serialize(new
        {
            Format = 1, WorldKind = world.RecordingKind, SessionId, Version = version, StartedUtc = DateTime.UtcNow, Language = language,
            Environment.OSVersion, Runtime = Environment.Version.ToString(), ProcessId = Environment.ProcessId,
            Consent = "Detailed local first-player playtest recording explicitly requested by the player.",
            InitialStateHash = Hash(initial), Includes = new[] { "player words", "notebook", "choices", "world decisions", "state deltas", "AI wording", "feedback" },
            Excludes = new[] { "API keys", "provider credentials", "unrelated files", "desktop screenshots" }
        }, Json));
        writer = new StreamWriter(new FileStream(Path.Combine(DirectoryPath, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        world.DecisionRecorded = decision => Record("decision", decision);
        world.TickCompleted = () => Capture("tick");
        Record("session-start", new { InitialTick = world.Tick, Language = language });
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public void Record(string kind, object? data)
    {
        if (!Healthy) return;
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(new { Sequence = ++sequence, Utc = DateTime.UtcNow, ElapsedMs = clock.ElapsedMilliseconds,
                Tick = world.Tick, Kind = kind, Data = data }, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Failure = ex.GetType().Name; }
    }
    public void Capture(string cause)
    {
        if (!Healthy) return;
        var saved = world.SaveJson(); var current = JsonNode.Parse(saved)!;
        var changes = StateDelta.Between(previous, current);
        if (changes.Count == 0) return;
        Record("state", new { Cause = cause, Hash = Hash(saved), Changes = changes });
        previous = current;
        if (++checkpoints % 96 != 0) return;
        try { File.WriteAllText(Path.Combine(DirectoryPath, $"checkpoint-{sequence:D8}.json"), saved); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Failure = ex.GetType().Name; }
    }
    public void Dispose()
    {
        if (disposed) return;
        Capture("session-end"); Record("session-end", new { DurationMs = clock.ElapsedMilliseconds });
        world.DecisionRecorded = null; world.TickCompleted = null;
        disposed = true;
        try { writer.Dispose(); }
        catch (IOException ex) { Failure = ex.GetType().Name; }
    }
}
