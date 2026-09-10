using System.Text.Json;

namespace DeepPremise.Core.City;

public sealed class CityTile
{
    public string Kind { get; set; } = "empty";
    public int Visits { get; set; }
    public int Level { get; set; } = 1;
    public bool Connected { get; set; }
}

public sealed class Citizen
{
    public int Id { get; set; }
    public int Home { get; set; }
    public int At { get; set; }
    public int Previous { get; set; }
    public int Destination { get; set; }
    public int Work { get; set; } = -1;
    public string Activity { get; set; } = "home";
    public string Preference { get; set; } = "park";
    public int Happiness { get; set; } = 65;
    public int Favorite { get; set; } = -1;
    public int Visits { get; set; }
    public List<int> Route { get; set; } = new();
}

public sealed record CityEvent(int Day, string Key, int Place = -1, int Value = 0);
public sealed class CityState
{
    public int Version { get; set; } = 9;
    public uint Seed { get; set; } = 31;
    public long Tick { get; set; }
    public int Funds { get; set; } = 260;
    public int LastIncome { get; set; }
    public int LastCost { get; set; }
    public List<CityTile> Tiles { get; set; } = new();
    public List<Citizen> People { get; set; } = new();
    public List<CityEvent> Journal { get; set; } = new();
    public int Day => (int)(Tick / 240) + 1;
}

// Simulation time, routing, migration, and place identity are independent of rendering.
public sealed class CityWorld : IRecordedWorld
{
    public const int Width = 18, Height = 12;
    public static readonly Dictionary<string, int> Prices = new()
    { ["road"] = 3, ["home"] = 30, ["workshop"] = 45, ["market"] = 35, ["park"] = 20, ["erase"] = 0 };
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private CityState state = new();
    public string RecordingKind => "city-v9";
    public long Tick => state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public CityWorld(uint seed = 31)
    {
        state.Seed = seed;
        for (int i = 0; i < Width * Height; i++) state.Tiles.Add(new CityTile { Kind = i % Width >= 16 ? "river" : "empty" });
        for (int x = 0; x < 16; x++) state.Tiles[6 * Width + x].Kind = "road";
        for (int y = 3; y <= 9; y++) state.Tiles[y * Width + 7].Kind = "road";
        Put(4, 5, "home"); Put(6, 5, "home"); Put(5, 7, "home");
        Put(8, 5, "workshop"); Put(9, 7, "market"); Put(6, 8, "park");
        Connect();
        foreach (int home in Enumerable.Range(0, state.Tiles.Count).Where(i => state.Tiles[i].Kind == "home"))
            for (int n = 0; n < 3; n++) AddCitizen(home);
        Jobs(); Note("welcome");
    }
    private void Put(int x, int y, string kind) => state.Tiles[y * Width + x].Kind = kind;
    public CityState Observe() => JsonSerializer.Deserialize<CityState>(SaveJson())!;
    public string SaveJson() => JsonSerializer.Serialize(state, Json);
    public static CityWorld LoadJson(string json)
    {
        var s = JsonSerializer.Deserialize<CityState>(json) ?? throw new InvalidDataException("Missing city state.");
        if (s.Version != 9 || s.Tiles.Count != Width * Height) throw new InvalidDataException("Unsupported city save.");
        return new CityWorld(s.Seed) { state = s };
    }
    private IEnumerable<int> Neighbors(int i)
    {
        int x = i % Width, y = i / Width;
        if (x > 0) yield return i - 1; if (x < Width - 1) yield return i + 1;
        if (y > 0) yield return i - Width; if (y < Height - 1) yield return i + Width;
    }
    private void Connect()
    {
        foreach (var t in state.Tiles) t.Connected = false;
        int entrance = 6 * Width;
        if (state.Tiles[entrance].Kind != "road") return;
        var queue = new Queue<int>(); queue.Enqueue(entrance); state.Tiles[entrance].Connected = true;
        while (queue.TryDequeue(out int i))
            foreach (int next in Neighbors(i))
            {
                var tile = state.Tiles[next];
                if (tile.Connected || tile.Kind is "empty" or "river") continue;
                tile.Connected = true;
                if (tile.Kind == "road") queue.Enqueue(next);
            }
    }
    public string Build(int index, string kind)
    {
        string result = "ok";
        if (index < 0 || index >= state.Tiles.Count || !Prices.ContainsKey(kind)) result = "invalid";
        else if (state.Tiles[index].Kind == "river" || index == 6 * Width) result = "protected";
        else if (kind == "erase" && state.People.Any(p => p.Home == index)) result = "occupied";
        else if (kind != "erase" && state.Tiles[index].Kind != "empty") result = "not_empty";
        else if (state.Funds < Prices[kind]) result = "no_funds";
        if (result == "ok")
        {
            state.Funds -= Prices[kind];
            if (kind == "erase" && Prices.TryGetValue(state.Tiles[index].Kind, out int refund)) state.Funds += refund / 2;
            state.Tiles[index] = new CityTile { Kind = kind == "erase" ? "empty" : kind };
            Connect(); Jobs();
            foreach (var p in state.People)
            {
                int target = p.Activity == "working" ? p.Work : p.Destination;
                if (target < 0 || state.Tiles[target].Kind is "empty" or "river") target = p.Home;
                SetDestination(p, target, target == p.Home ? "home" : p.Activity);
            }
        }
        Trace("build", result, new { index, kind, state.Funds }); return result;
    }
    private void Jobs()
    {
        var workplaces = Enumerable.Range(0, state.Tiles.Count).Where(i => state.Tiles[i].Connected && state.Tiles[i].Kind is "workshop" or "market").ToList();
        var used = new Dictionary<int, int>();
        foreach (var p in state.People)
        {
            p.Work = workplaces.Where(i => used.GetValueOrDefault(i) < (state.Tiles[i].Kind == "workshop" ? 8 : state.Tiles[i].Level == 2 ? 6 : 4))
                .OrderBy(i => Distance(p.Home, i)).ThenBy(i => i).FirstOrDefault(-1);
            if (p.Work >= 0) used[p.Work] = used.GetValueOrDefault(p.Work) + 1;
        }
    }
    private static int Distance(int a, int b) => Math.Abs(a % Width - b % Width) + Math.Abs(a / Width - b / Width);
    private List<int> Path(int start, int goal)
    {
        if (start == goal) return new();
        var queue = new Queue<int>(); var parent = new Dictionary<int, int> { [start] = -1 }; queue.Enqueue(start);
        while (queue.TryDequeue(out int i))
        {
            foreach (int n in Neighbors(i))
            {
                if (parent.ContainsKey(n) || (n != goal && state.Tiles[n].Kind != "road")) continue;
                parent[n] = i;
                if (n == goal)
                {
                    var route = new List<int>(); for (int at = goal; at != start; at = parent[at]) route.Add(at);
                    route.Reverse(); return route;
                }
                queue.Enqueue(n);
            }
        }
        return new();
    }
    private void SetDestination(Citizen p, int target, string activity)
    {
        p.Route = Path(p.At, target); p.Destination = target;
        if (activity == "isolated") activity = target == p.Home ? "home" : target == p.Work ? "working" : "visiting";
        p.Activity = p.At != target && p.Route.Count == 0 ? "isolated" : activity;
        Trace("journey", p.Activity, new { p.Id, p.At, target, Route = p.Route.ToArray() });
    }
    public void Run(int ticks)
    {
        for (int n = 0; n < ticks; n++)
        {
            state.Tick++; int time = (int)(state.Tick % 240);
            foreach (var p in state.People)
            {
                if (time == 25) SetDestination(p, p.Work < 0 ? p.Home : p.Work, p.Work < 0 ? "unemployed" : "working");
                if (time == 130)
                {
                    var destinations = Enumerable.Range(0, state.Tiles.Count).Where(i => state.Tiles[i].Connected && state.Tiles[i].Kind is "park" or "market").ToList();
                    int target = destinations.OrderByDescending(i => (state.Tiles[i].Kind == p.Preference ? 8 : 0) + (p.Favorite == i ? 3 : 0) + (state.Tiles[i].Level == 2 ? 2 : 0) - Distance(p.Home, i) / 3).ThenBy(i => i).FirstOrDefault(p.Home);
                    SetDestination(p, target, target == p.Home ? "home" : "visiting");
                }
                if (time == 205) SetDestination(p, p.Home, "home");
                p.Previous = p.At;
                if (state.Tick % 3 == 0 && p.Route.Count > 0)
                {
                    p.At = p.Route[0]; p.Route.RemoveAt(0);
                    if (p.Route.Count == 0 && p.Activity == "visiting")
                    {
                        state.Tiles[p.At].Visits++; p.Visits++; p.Favorite = p.At;
                        Trace("visit", "preference_and_distance", new { p.Id, p.At, p.Preference });
                    }
                }
            }
            if (time == 0) NextDay();
            TickCompleted?.Invoke();
        }
    }
    private void NextDay()
    {
        int employed = state.People.Count(p => p.Work >= 0 && state.Tiles[p.Home].Connected);
        int eveningTrade = state.People.Count(p => p.Visits > 0 && p.Favorite >= 0 && state.Tiles[p.Favorite].Kind == "market" && state.Tiles[p.Favorite].Level == 2) * 2;
        state.LastIncome = employed * 3 + state.People.Count + eveningTrade;
        state.LastCost = state.Tiles.Count(t => t.Kind == "park") * 2 + state.Tiles.Count(t => t.Kind is "workshop" or "market") * 3;
        state.Funds = Math.Max(0, state.Funds + state.LastIncome - state.LastCost);
        foreach (var p in state.People)
        {
            bool noise = Enumerable.Range(0, state.Tiles.Count).Any(i => state.Tiles[i].Kind == "workshop" && Distance(p.Home, i) <= 2);
            bool gathering = p.Visits > 0 && p.Favorite >= 0 && state.Tiles[p.Favorite].Kind == "park" && state.Tiles[p.Favorite].Level == 2;
            int target = 40 + (p.Work >= 0 ? 20 : 0) + (p.Visits > 0 ? 25 : 0) + (gathering ? 5 : 0) - (noise ? 15 : 0) - (!state.Tiles[p.Home].Connected ? 30 : 0);
            p.Happiness = Math.Clamp((p.Happiness + target) / 2, 0, 100); p.Visits = 0;
        }
        foreach (int i in Enumerable.Range(0, state.Tiles.Count))
        {
            var tile = state.Tiles[i];
            if (tile.Kind is "market" or "park" && tile.Level == 1 && tile.Visits >= 18)
            { tile.Level = 2; Note(tile.Kind == "park" ? "festival" : "night_market", i); }
            if (tile.Kind == "home" && tile.Level == 1 && state.People.Any(p => p.Home == i && p.Happiness >= 75) && state.Day >= 4)
            { tile.Level = 2; Note("home_grown", i); }
        }
        Jobs();
        int capacity = state.Tiles.Where(t => t.Kind == "home" && t.Connected).Sum(t => t.Level == 1 ? 4 : 6);
        if (state.People.Count < Math.Min(72, capacity) && employed >= state.People.Count - 2 && state.People.Average(p => p.Happiness) >= 55)
        {
            int home = Enumerable.Range(0, state.Tiles.Count).FirstOrDefault(i => state.Tiles[i].Kind == "home" && state.Tiles[i].Connected && state.People.Count(p => p.Home == i) < (state.Tiles[i].Level == 1 ? 4 : 6), -1);
            if (home >= 0) { AddCitizen(home); Note("arrival", home); Jobs(); }
        }
        Note("budget", -1, state.LastIncome - state.LastCost);
        Trace("daily", "jobs_visits_noise_and_capacity", new { state.LastIncome, state.LastCost, Population = state.People.Count, Happiness = state.People.Select(p => new { p.Id, p.Happiness }) });
    }
    private void AddCitizen(int home)
    {
        int id = state.People.Count;
        state.People.Add(new Citizen { Id = id, Home = home, At = home, Previous = home, Destination = home, Preference = (id + state.Seed) % 3 == 0 ? "market" : "park" });
    }
    private void Note(string key, int place = -1, int value = 0)
    { state.Journal.Add(new(state.Day, key, place, value)); if (state.Journal.Count > 80) state.Journal.RemoveAt(0); }
    private void Trace(string system, string reason, object facts) => DecisionRecorded?.Invoke(new(state.Tick, "city", system, reason, facts));
}
