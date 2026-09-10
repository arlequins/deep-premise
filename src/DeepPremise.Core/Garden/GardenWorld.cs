using System.Text.Json;

namespace DeepPremise.Core.Garden;

public sealed class GardenObject
{
    public int Id { get; set; }
    public string Kind { get; set; } = "snail";
    public double X { get; set; }
    public double Y { get; set; }
    public int Warmth { get; set; }
    public int Growth { get; set; }
    public double Glow { get; set; }
    public int Following { get; set; } = -1;
    public string Behavior { get; set; } = "resting";
    public int Age { get; set; } = 1200;
    public int Generation { get; set; }
    public string Hue { get; set; } = "amber";
    public int Dew { get; set; }
    public int Pollen { get; set; }
    public int Care { get; set; }
    public int Cooldown { get; set; }
    public bool Away { get; set; }
}

public sealed class GardenState
{
    public int Version { get; set; } = 11;
    public long Tick { get; set; }
    public bool Night { get; set; }
    public bool DiscoveredGlow { get; set; }
    public bool DiscoveredFollowing { get; set; }
    public bool DiscoveredSeed { get; set; }
    public bool DiscoveredBloom { get; set; }
    public List<GardenObject> Objects { get; set; } = new();
    public Dictionary<string,int> Pouch { get; set; } = new() { ["snail"] = 2, ["stone"] = 2, ["grass"] = 3, ["seed"] = 0 };
    public List<GardenObject> Stored { get; set; } = new();
    public List<string> Discoveries { get; set; } = new();
    public int Trips { get; set; }
    public int Explorer { get; set; } = -1;
    public int ExpeditionTicks { get; set; }
    public int ExpeditionDuration { get; set; }
    public int Days => (int)(Tick / 1200) + 1;
}

// Fixed-step deterministic ecology. Coordinates belong to the garden, not a viewport.
public sealed partial class GardenWorld : IRecordedWorld
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private GardenState state = new();
    public string RecordingKind => "garden-v11";
    public long Tick => state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public GardenWorld()
    {
        EnsureSupplies();
        state.Objects.Add(new() { Id=0,Kind="stone",X=.46,Y=.49 });
        state.Objects.Add(new() { Id=1,Kind="snail",X=.31,Y=.56 });
        state.Objects.Add(new() { Id=2,Kind="grass",X=.63,Y=.59 });
    }
    public GardenState Observe() => JsonSerializer.Deserialize<GardenState>(SaveJson())!;
    public string SaveJson() => JsonSerializer.Serialize(state,Json);
    public static GardenWorld LoadJson(string json)
    {
        var s=JsonSerializer.Deserialize<GardenState>(json) ?? throw new InvalidDataException("Missing garden.");
        if(s.Version is not (10 or 11) || s.Objects.Count>36 || s.Objects.Concat(s.Stored).Select(o=>o.Id).Distinct().Count()!=s.Objects.Count+s.Stored.Count || s.Objects.Concat(s.Stored).Any(o=>!double.IsFinite(o.X)||!double.IsFinite(o.Y)||o.X<.04||o.X>.96||o.Y<.08||o.Y>.92||!Kinds.Contains(o.Kind))) throw new InvalidDataException("Invalid garden save.");
        s.Version=11;var world=new GardenWorld { state=s };world.EnsureSupplies();return world;
    }
    public void ToggleLight()
    { state.Night=!state.Night; Trace("light",new{state.Night}); }
    public bool Move(int id,double x,double y)
    {
        var o=state.Objects.FirstOrDefault(o=>o.Id==id);
        if(o==null||o.Away||!double.IsFinite(x)||!double.IsFinite(y))return false;
        o.X=Math.Clamp(x,.04,.96);o.Y=Math.Clamp(y,.08,.92);o.Following=-1;
        Trace("move",new{id,o.X,o.Y});return true;
    }
    public int Place(string kind,double x,double y)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||state.Pouch.GetValueOrDefault(kind)<=0||state.Objects.Count>=36)return -1;
        var item=state.Stored.FirstOrDefault(o=>o.Kind==kind)??new GardenObject{Id=NextId(),Kind=kind};
        state.Stored.Remove(item);int id=item.Id;item.X=Math.Clamp(x,.04,.96);item.Y=Math.Clamp(y,.08,.92);item.Following=-1;
        state.Pouch[kind]--;state.Objects.Add(item);
        Trace("place",new{id,kind,x,y});return id;
    }
    public void Run(int ticks)
    {
        for(int i=0;i<ticks;i++)
        {
            state.Tick++;
            // Decisions read a common snapshot, so list iteration never changes a neighbor's stimulus.
            var previous=state.Objects.Where(o=>!o.Away).Select(o=>new GardenObject {Id=o.Id,Kind=o.Kind,X=o.X,Y=o.Y,Glow=o.Glow,Growth=o.Growth,Hue=o.Hue,Age=o.Age,Dew=o.Dew,Pollen=o.Pollen,Cooldown=o.Cooldown,Generation=o.Generation}).ToArray();
            foreach(var o in state.Objects)
            {
                if(o.Away)continue;
                o.Age++;if(o.Cooldown>0)o.Cooldown--;
                string old=o.Behavior;int following=o.Following;
                if(o.Kind=="stone") {o.Glow=state.Night?.35:0;o.Behavior=state.Night?"warm":"cool";}
                if(o.Kind=="snail")
                {
                    var stone=previous.Where(p=>p.Kind=="stone").OrderBy(p=>Distance(o,p)).FirstOrDefault();
                    bool near=state.Night&&stone!=null&&Distance(o,stone)<.12;
                    o.Warmth=Math.Clamp(o.Warmth+(near?1:-1),0,120);
                    if(o.Warmth>=65 && o.Glow<1)
                    {o.Glow=1;if(!state.DiscoveredGlow){state.DiscoveredGlow=true;Trace("discovery",new{Key="glow",o.Id});}}
                    if(state.Night&&o.Glow<1&&stone!=null)
                    {o.Behavior=near?"warming":"seeking_warmth";if(!near)Walk(o,stone.X,stone.Y,.0015);}
                    else
                    {
                        o.Behavior=o.Glow>=1?"glowing":"wandering";
                        double phase=state.Tick*.005+o.Id*1.9;
                        double x=.5+Math.Sin(phase)*.29,y=.5+Math.Cos(phase*.71)*.25;
                        var moss=previous.Where(p=>p.Kind=="moss").OrderBy(p=>Distance(o,p)).FirstOrDefault();
                        if(moss!=null&&state.Night&&o.Cooldown==0){x=moss.X;y=moss.Y;o.Behavior="nesting";}
                        Walk(o,x,y,o.Age<600?.0008:.0014);
                    }
                    if(o.Glow>=1&&Math.Sqrt(Math.Pow(o.X-.87,2)+Math.Pow(o.Y-.25,2))<.14&&!state.DiscoveredSeed)
                    {state.DiscoveredSeed=true;state.Pouch["seed"]=2;Trace("discovery",new{Key="seed",o.Id});}
                }
                if(o.Kind=="grass")
                {
                    var light=previous.Where(p=>p.Kind=="snail"&&p.Glow>=1 || p.Kind=="seed"&&p.Growth>=100)
                        .Where(p=>Distance(o,p)<.36).OrderBy(p=>Distance(o,p)).ThenBy(p=>p.Id).FirstOrDefault();
                    o.Following=light?.Id??-1;o.Behavior=light==null?"rooted":"following";
                    if(light!=null)
                    {
                        double angle=o.Id*2.4;
                        Walk(o,light.X+Math.Cos(angle)*.048,light.Y+Math.Sin(angle)*.048,.0018);
                        if(!state.DiscoveredFollowing){state.DiscoveredFollowing=true;Trace("discovery",new{Key="following",o.Id,Target=light.Id});}
                    }
                }
                if(o.Kind=="seed")
                {
                    bool sheltered=previous.Any(p=>p.Kind=="grass"&&Distance(o,p)<.14);
                    bool warm=state.Night&&previous.Any(p=>p.Kind=="stone"&&Distance(o,p)<.18);
                    bool wet=previous.Any(p=>p.Kind=="pond"&&Distance(o,p)<.16)||o.Dew>0;
                    bool moon=state.Night&&previous.Any(p=>p.Kind=="moonstone"&&Distance(o,p)<.18);
                    if(sheltered&&(warm||wet||moon)&&o.Growth<100)
                    {o.Growth++;o.Hue=moon?"moon":wet?"blue":"amber";}
                    o.Behavior=o.Growth>=100?"blooming":sheltered&&(warm||wet||moon)?"growing":"waiting";o.Glow=o.Growth>=100?.8:0;
                    if(o.Growth>=100&&!state.DiscoveredBloom){state.DiscoveredBloom=true;Trace("discovery",new{Key="bloom",o.Id});}
                    if(o.Growth>=100)Discover("flower_"+o.Hue);
                }
                ExtendedBehavior(o,previous);
                if(old!=o.Behavior||following!=o.Following)Trace("behavior",new{o.Id,From=old,To=o.Behavior,o.Following,o.Warmth,o.Growth});
            }
            AdvanceEcosystem(previous);
            TickCompleted?.Invoke();
        }
    }
    private static double Distance(GardenObject a,GardenObject b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
    private static void Walk(GardenObject o,double x,double y,double step)
    { double dx=x-o.X,dy=y-o.Y,d=Math.Sqrt(dx*dx+dy*dy);if(d>step){o.X=Math.Clamp(o.X+dx/d*step,.04,.96);o.Y=Math.Clamp(o.Y+dy/d*step,.08,.92);} }
    private void Trace(string decision,object facts)=>DecisionRecorded?.Invoke(new(state.Tick,"garden",decision,"local_environment",facts));
}
