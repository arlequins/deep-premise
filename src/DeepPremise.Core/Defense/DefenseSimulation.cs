using System.Text.Json;

namespace DeepPremise.Core.Defense;

public sealed class DefenseState
{
    public int Version { get; set; } = 6;
    public int Seed { get; set; } = 1;
    public long Tick { get; set; }
    public int Stock { get; set; } = 8;
    public int Integrity { get; set; } = 6;
    public int Workers { get; set; } = 3;
    public int Route { get; set; }
    public int Guard { get; set; } = 1;
    public int Decoy { get; set; } = -1;
    public int ScoutLane { get; set; } = -1;
    public int Evidence { get; set; } = -1;
    public int RaidLane { get; set; } = -1;
    public int Raids { get; set; }
    public bool Council { get; set; }
    public bool CouncilDone { get; set; }
    public bool Ended { get; set; }
    public List<string> Events { get; set; } = new();
}

// Enemy intelligence is acquired on the map and carried home, never read from future orders.
public sealed class DefenseSimulation : IRecordedWorld
{
    private DefenseState state;
    public DefenseSimulation(int seed = 1) { state = new() { Seed = seed, Route = Math.Abs(seed % 3) }; Note("opening"); }
    private DefenseSimulation(DefenseState saved) { state = saved; }
    public string RecordingKind => "defense-v6";
    public long Tick => state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public DefenseState Observe() => JsonSerializer.Deserialize<DefenseState>(SaveJson())!;
    public string SaveJson() => JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    public static DefenseSimulation LoadJson(string json)
    {
        var s = JsonSerializer.Deserialize<DefenseState>(json) ?? throw new InvalidDataException("Missing state");
        if (s.Version != 6 || s.Stock < 0 || s.Workers < 3 || s.Workers > 6 || s.Route is < 0 or > 2 || s.Guard is < 0 or > 2 || s.Tick < 0)
            throw new InvalidDataException("Invalid defense state");
        return new(s);
    }
    private void Note(string key)
    {
        state.Events.Add($"{state.Tick}|{key}");
        if (state.Events.Count > 8) state.Events.RemoveAt(0);
        DecisionRecorded?.Invoke(new(state.Tick, "defense", key, "Resolved from physical lane, timing, resources and witnessed evidence", new { state.Route, state.Guard, state.Evidence, state.Stock }));
    }
    public bool Order(string action, int lane = 0)
    {
        if (state.Ended || lane is < 0 or > 2) return false;
        switch (action)
        {
            case "route": state.Route = lane; break;
            case "guard": state.Guard = lane; break;
            case "decoy" when state.Stock >= 3: state.Stock -= 3; state.Decoy = lane; break;
            case "recruit" when state.Stock >= 12 && state.Workers < 6: state.Stock -= 12; state.Workers++; break;
            case "repair" when state.Stock >= 5 && state.Integrity < 6: state.Stock -= 5; state.Integrity++; break;
            case "vote" when state.Council:
                // Player breaks a public split: the quartermaster favors capture, the pathfinder deception.
                state.Council = false; state.CouncilDone = true;
                if (lane == 0) { state.Guard = state.ScoutLane; Note("vote_capture"); }
                else { state.Decoy = state.ScoutLane; Note("vote_deceive"); }
                break;
            default: return false;
        }
        Note(action); return true;
    }
    public void Run(int ticks) { for (var i = 0; i < ticks && !state.Ended; i++) Step(); }
    private void Step()
    {
        state.Tick++;
        var phase = state.Tick % 120;
        if (state.Tick % 12 == 0) { state.Stock += state.Workers; Note("delivery"); }
        if (phase == 20)
        {
            state.ScoutLane = (int)((state.Seed + state.Tick / 120) % 3 + 3) % 3;
            state.Evidence = -1; Note("scout_arrived");
        }
        if (phase >= 30 && phase <= 60 && state.ScoutLane >= 0)
        {
            if (state.Guard == state.ScoutLane) { state.ScoutLane = -1; state.Evidence = -1; Note("scout_caught"); }
            else if (state.Decoy == state.ScoutLane) state.Evidence = state.Decoy;
            else if (state.Route == state.ScoutLane && state.Tick % 12 == 0) { state.Evidence = state.Route; Note("delivery_seen"); }
        }
        if (phase == 45 && !state.CouncilDone && state.ScoutLane >= 0) { state.Council = true; Note("council"); }
        if (phase == 65)
        {
            if (state.ScoutLane >= 0) Note(state.Evidence >= 0 ? "report_escaped" : "empty_report");
            state.ScoutLane = -1; state.Council = false;
        }
        if (phase == 85) { state.RaidLane = state.Evidence; if (state.RaidLane >= 0) Note("raid_approaching"); else Note("no_target"); }
        if (phase == 105)
        {
            if (state.RaidLane >= 0)
            {
                if (state.Guard == state.RaidLane) { state.Stock += 2; Note("raid_blocked"); }
                else if (state.RaidLane != state.Route) Note("raid_empty_lane");
                else { state.Stock = Math.Max(0, state.Stock - 8); state.Integrity -= 2; Note("raid_hit"); }
            }
            state.Raids++; state.RaidLane = -1; state.Decoy = -1;
            if (state.Integrity <= 0) { state.Ended = true; Note("lost"); }
            else if (state.Raids >= 6) { state.Ended = true; Note("won"); }
        }
        TickCompleted?.Invoke();
    }
}
