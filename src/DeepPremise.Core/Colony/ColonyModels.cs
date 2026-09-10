namespace DeepPremise.Core.Colony;

public sealed record BuildingDefinition(string Id, string Name, int Width, int Height, int Wood, int Stone, int Tools, int Work, string Purpose);
public sealed class ColonyWorld
{
    public int Version { get; set; } = 4;
    public uint RandomState { get; set; } = 1;
    public long Tick { get; set; }
    public int NextId { get; set; } = 1;
    public long NextArrival { get; set; } = 300;
    public int ImprintThreshold { get; set; } = 3;
    public int TotalTools { get; set; }
    public string Landscape { get; set; } = "green";
    public string Perk { get; set; } = "";
    public List<string> PerkChoices { get; set; } = [];
    public bool Ended { get; set; }
    public List<string> Terrain { get; set; } = [];
    public Dictionary<string,int> Stock { get; set; } = new() { ["wood"] = 40, ["stone"] = 20, ["food"] = 18, ["tools"] = 0 };
    public List<ColonyPawn> Pawns { get; set; } = [];
    public List<ColonyBuilding> Buildings { get; set; } = [];
    public List<ColonyResource> Resources { get; set; } = [];
    public List<ColonyNotice> Notices { get; set; } = [];
}
public sealed class ColonyPawn
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Energy { get; set; } = 100;
    public int Hunger { get; set; }
    public string Priority { get; set; } = "auto";
    public ColonyJob? Job { get; set; }
}
public sealed class ColonyJob
{
    public string Kind { get; set; } = "";
    public int Target { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Progress { get; set; }
    public int Required { get; set; }
}
public sealed class ColonyBuilding
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Work { get; set; }
    public bool Built { get; set; }
    public bool Producing { get; set; } = true;
    public int Cycle { get; set; }
    public int CompletedCycles { get; set; }
    public bool CyclePaid { get; set; }
    public bool NoticedImprint { get; set; }
}
public sealed class ColonyResource
{
    public int Id { get; set; }
    public string Kind { get; set; } = "wood";
    public int X { get; set; }
    public int Y { get; set; }
    public int Amount { get; set; }
    public bool Designated { get; set; }
}
public sealed record ColonyNotice(long Tick, string Text, string Kind = "ordinary", int X = 12, int Y = 9);
public sealed record PawnView(int Id,string Name,int X,int Y,int Energy,int Hunger,string Priority,string Job,string Target,int Progress,int Required,int TargetX,int TargetY);
public sealed record BuildingView(int Id,string Kind,int X,int Y,bool Built,int Work,int Required,bool Producing,int Cycle,int Cycles);
public sealed record ResourceView(int Id,string Kind,int X,int Y,int Amount,bool Designated);
public sealed record ColonyView(long Tick,IReadOnlyList<string> Terrain,IReadOnlyDictionary<string,int> Stock,int Capacity,
    IReadOnlyList<PawnView> Pawns,IReadOnlyList<BuildingView> Buildings,IReadOnlyList<ResourceView> Resources,IReadOnlyList<ColonyNotice> Notices,string Goal,int GoalIndex,int TotalTools,string Landscape,string Perk,IReadOnlyList<string> PerkChoices,bool Ended,bool Cold);
