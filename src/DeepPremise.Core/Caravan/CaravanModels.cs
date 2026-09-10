namespace DeepPremise.Core.Caravan;

public sealed class CaravanWorld
{
    public int Version { get; set; } = 5;
    public uint RandomState { get; set; }
    public long Tick { get; set; }
    public string Condition { get; set; } = "clear";
    public string AnchorCargo { get; set; } = "wood";
    public string Charter { get; set; } = "";
    public List<string> Charters { get; set; } = [];
    public bool Ended { get; set; }
    public int Delivered { get; set; }
    public int Journeys { get; set; }
    public Dictionary<string,int> Stock { get; set; } = new() { ["food"]=16,["wood"]=0,["metal"]=4 };
    public Dictionary<string,int> Gear { get; set; } = new() { ["packs"]=0,["wheels"]=0,["lantern"]=0,["caravan"]=0 };
    public List<Waystation> Sites { get; set; } = [];
    public List<Carrier> Crew { get; set; } = [];
    public List<RememberedRoad> Roads { get; set; } = [];
    public List<TravelNotice> Notices { get; set; } = [];
    public CraftOrder? Craft { get; set; }
}
public sealed class Waystation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string Resource { get; set; } = "";
    public int Supply { get; set; }
    public bool Known { get; set; }
}
public sealed class Carrier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Trait { get; set; } = "steady";
    public List<int> Route { get; set; } = [];
    public bool Recalling { get; set; }
    public string Phase { get; set; } = "idle";
    public int From { get; set; }
    public int To { get; set; }
    public int At { get; set; }
    public int Progress { get; set; }
    public int Duration { get; set; } = 1;
    public int Stop { get; set; }
    public int Hunger { get; set; }
    public int Rest { get; set; }
    public int LastTrip { get; set; }
    public long Departed { get; set; }
    public Dictionary<string,int> Bag { get; set; } = new() { ["food"]=0,["wood"]=0,["metal"]=0 };
}
public sealed class RememberedRoad
{
    public int A { get; set; }
    public int B { get; set; }
    public int Base { get; set; }
    public int Memory { get; set; }
    public int Traversals { get; set; }
    public long LastUsed { get; set; }
}
public sealed class CraftOrder { public string Kind { get; set; } = ""; public int Progress { get; set; } public int Required { get; set; } }
public sealed record TravelNotice(long Tick,string Text,string Kind="ordinary",int Site=0);
public sealed record GearRecipe(string Id,string Name,int Food,int Wood,int Metal,int Work,int Limit,string Description);
public sealed record SiteView(int Id,string Name,float X,float Y,string Resource,int Supply,bool Known);
public sealed record CarrierView(int Id,string Name,string Trait,string Phase,int From,int To,int At,float Progress,IReadOnlyList<int> Route,IReadOnlyDictionary<string,int> Bag,int Capacity,int LastTrip,int Hunger,bool Recalling);
public sealed record RoadView(int A,int B,int Cost,int Original,int Traversals);
public sealed record CaravanView(long Tick,string Condition,string Charter,IReadOnlyList<string> Charters,bool Ended,int Delivered,int Journeys,
    IReadOnlyDictionary<string,int> Stock,IReadOnlyDictionary<string,int> Gear,IReadOnlyList<SiteView> Sites,IReadOnlyList<CarrierView> Crew,IReadOnlyList<RoadView> Roads,
    IReadOnlyList<TravelNotice> Notices,string Craft,int CraftProgress,int CraftRequired,string Goal,int Stage);
