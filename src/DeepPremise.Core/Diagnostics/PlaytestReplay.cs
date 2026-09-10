using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeepPremise.Core.Diagnostics;

public sealed record PlaytestRestore(string StateJson, long Sequence, long Tick, int VerifiedStates, bool TruncatedTail);

public static class PlaytestReplay
{
    public static PlaytestRestore Restore(string directory, long throughSequence = long.MaxValue)
    {
        var initial = File.ReadAllText(Path.Combine(directory, "initial-state.json"));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")))!;
        if (manifest["InitialStateHash"]?.GetValue<string>() != PlaytestRecorder.Hash(initial))
            throw new InvalidDataException("The initial state hash does not match the session manifest.");
        var state = JsonNode.Parse(initial)!;
        var sequence = 0L; var verified = 0; var truncated = false;
        using var reader = new StreamReader(new FileStream(Path.Combine(directory, "events.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        while (reader.ReadLine() is { } line)
        {
            JsonNode entry;
            try { entry = JsonNode.Parse(line)!; }
            catch (JsonException) when (reader.EndOfStream) { truncated = true; break; }
            var next = entry["Sequence"]!.GetValue<long>();
            if (next > throughSequence) break;
            if (next != sequence + 1) throw new InvalidDataException("The event sequence is incomplete.");
            sequence = next;
            if (entry["Kind"]!.GetValue<string>() != "state") continue;
            var changes = entry["Data"]!["Changes"]!.Deserialize<List<StateChange>>()!;
            state = StateDelta.Apply(state, changes);
            var serialized = state.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (PlaytestRecorder.Hash(serialized) != entry["Data"]!["Hash"]!.GetValue<string>())
                throw new InvalidDataException($"State hash mismatch at sequence {sequence}.");
            verified++;
        }
        var result = state.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        IRecordedWorld loaded = state["Version"]!.GetValue<int>() switch
        {
            11 => Garden.GardenWorld.LoadJson(result),
            10 => Garden.GardenWorld.LoadJson(result),
            9 => City.CityWorld.LoadJson(result),
            8 => Habits.HabitWorld.LoadJson(result),
            7 => Defense.SalvageRun.LoadJson(result),
            6 => Defense.DefenseSimulation.LoadJson(result),
            5 => Caravan.CaravanSimulation.LoadJson(result),
            4 => Colony.ColonySimulation.LoadJson(result),
            _ => SimulationRunner.LoadJson(result)
        };
        return new(result, sequence, loaded.Tick, verified, truncated);
    }
}
