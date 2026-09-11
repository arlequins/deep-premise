using System.Text.Json;

namespace DeepPremise.Core.FirstLight;

public sealed class LightEntity
{
    public int Id { get; set; }
    public string Kind { get; set; } = "plant";
    public double X { get; set; }
    public double Y { get; set; }
    public int Age { get; set; }
    public int Hunger { get; set; }
    public int Meals { get; set; }
    public int Fruit { get; set; }
    public int Cooldown { get; set; }
    public int Children { get; set; }
    public int BirthCooldown { get; set; }
    public bool Alive { get; set; } = true;
    public string Activity { get; set; } = "sleeping";
}

public sealed class LightMemory
{
    public LightEntity Entity { get; set; } = new();
    public long SeenAt { get; set; }
}
public sealed record LightEvent(long Tick, string Key, double X, double Y);
public sealed class LightState
{
    public int Version { get; set; } = 12;
    public long Tick { get; set; }
    public double LightX { get; set; } = .48;
    public double LightY { get; set; } = .52;
    public bool LightOn { get; set; } = true;
    public bool HunterArrived { get; set; }
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public int IndependentTicks { get; set; }
    public List<LightEntity> Entities { get; set; } = new();
    public List<LightMemory> Memories { get; set; } = new();
    public List<LightEvent> Events { get; set; } = new();
}

// One intervention: light freezes every affected entity, including its hunger and resources.
public sealed class LightWorld : IRecordedWorld
{
    public const double Aspect = 1.6, Radius = .175;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private LightState state = new();
    public string RecordingKind => "first-light-v12";
    public long Tick => state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public LightWorld()
    {
        state.Entities.Add(new() { Id=0,Kind="egg",X=.48,Y=.52 });
        var places=new[]{(.35,.49),(.55,.68),(.68,.42),(.3,.72),(.69,.72),(.39,.25)};
        foreach(var (x,y) in places)state.Entities.Add(new(){Id=state.Entities.Count,Kind="plant",X=x,Y=y});
        Remember();
    }
    public LightState Observe()=>JsonSerializer.Deserialize<LightState>(SaveJson())!;
    public string SaveJson()=>JsonSerializer.Serialize(state,Json);
    public static LightWorld LoadJson(string json)
    {
        var loaded=JsonSerializer.Deserialize<LightState>(json)??throw new InvalidDataException("Missing light world.");
        if(loaded.Version!=12||loaded.Entities.Count>20||loaded.Entities.Select(e=>e.Id).Distinct().Count()!=loaded.Entities.Count||loaded.Entities.Any(e=>!double.IsFinite(e.X)||!double.IsFinite(e.Y)||e.X<0||e.X>1||e.Y<0||e.Y>1))throw new InvalidDataException("Invalid light world.");
        return new LightWorld{state=loaded};
    }
    public static double Distance(double x,double y,double a,double b)=>Math.Sqrt(Math.Pow((x-a)*Aspect,2)+Math.Pow(y-b,2));
    public static bool IsLit(LightState s,LightEntity e)=>s.LightOn&&Distance(s.LightX,s.LightY,e.X,e.Y)<=Radius;
    public void Aim(double x,double y,bool on=true)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y))return;
        state.LightX=Math.Clamp(x,0,1);state.LightY=Math.Clamp(y,0,1);state.LightOn=on;Remember();
    }
    private bool Lit(LightEntity e)=>IsLit(state,e);
    public void Run(int ticks)
    {
        for(int i=0;i<ticks;i++)
        {
            if(state.Won||state.Lost)break;
            state.Tick++;
            var active=state.Entities.Where(e=>e.Alive).ToArray();
            foreach(var e in active)
            {
                if(Lit(e))continue;
                e.Age++;if(e.Cooldown>0)e.Cooldown--;if(e.BirthCooldown>0)e.BirthCooldown--;
                if(e.Kind=="plant")
                {
                    e.Activity=e.Age<70?"stone":e.Age<180?"moss":e.Age<850?"fruit":"forest";
                    if(e.Age>=180&&e.Age%65==0)e.Fruit=Math.Min(4,e.Fruit+1);
                }
                if(e.Kind=="egg"&&e.Age>=65)
                {e.Kind="life";e.Age=0;e.Activity="hungry";Event("birth",e);}
                if(e.Kind=="life")Life(e);
                if(e.Kind=="hunter")Hunter(e);
            }
            if(!state.HunterArrived&&state.Entities.Any(e=>e.Alive&&e.Kind=="life"&&e.Meals>=2))
            {
                state.HunterArrived=true;
                var hunter=new LightEntity{Id=state.Entities.Max(e=>e.Id)+1,Kind="hunter",X=.88,Y=.36};state.Entities.Add(hunter);Event("hunter",hunter);
            }
            int adults=state.Entities.Count(e=>e.Alive&&e.Kind=="life"&&e.Meals>=3);
            state.IndependentTicks=adults>=3&&!state.LightOn?state.IndependentTicks+1:0;
            if(state.IndependentTicks>=240){state.Won=true;Event("independent",state.Entities.First(e=>e.Kind=="life"&&e.Alive));}
            if(!state.Entities.Any(e=>e.Alive&&e.Kind is "life" or "egg")){state.Lost=true;Event("lost",state.Entities[0]);}
            Remember();TickCompleted?.Invoke();
        }
    }
    private bool Sheltered(LightEntity e)=>state.Entities.Any(p=>p.Kind=="plant"&&p.Age>=850&&Distance(e.X,e.Y,p.X,p.Y)<.09);
    private void Life(LightEntity e)
    {
        e.Hunger++;
        if(e.Hunger>=720){e.Alive=false;Event("starved",e);return;}
        var hunter=state.Entities.FirstOrDefault(p=>p.Kind=="hunter"&&p.Alive);
        var forest=state.Entities.Where(p=>p.Kind=="plant"&&p.Age>=850).OrderBy(p=>Distance(e.X,e.Y,p.X,p.Y)).FirstOrDefault();
        if(hunter!=null&&Distance(e.X,e.Y,hunter.X,hunter.Y)<.23&&forest!=null)
        {e.Activity="sheltering";Walk(e,forest.X,forest.Y,.003);}
        else
        {
            var food=state.Entities.Where(p=>p.Kind=="plant"&&p.Fruit>0&&!Lit(p)).OrderBy(p=>Distance(e.X,e.Y,p.X,p.Y)).ThenBy(p=>p.Id).FirstOrDefault();
            e.Activity=food==null?"hungry":"foraging";
            if(food!=null)
            {
                Walk(e,food.X,food.Y,.002);
                if(Distance(e.X,e.Y,food.X,food.Y)<.055&&e.Cooldown==0)
                {
                    food.Fruit--;e.Hunger=0;e.Meals++;e.Cooldown=90;Event(e.Meals==3?"grown":"fed",e);
                }
            }
        }
        if(e.Meals>=4&&e.Children<2&&e.BirthCooldown==0&&state.Entities.Count<20&&state.Entities.Count(p=>p.Alive&&p.Kind is "life" or "egg")<5)
        {
            var child=new LightEntity{Id=state.Entities.Max(p=>p.Id)+1,Kind="egg",X=Math.Clamp(e.X+(e.Children==0?.035:-.035),.05,.95),Y=Math.Clamp(e.Y+.035,.05,.95)};
            state.Entities.Add(child);e.Children++;e.BirthCooldown=180;Event("new_seed",child);
        }
    }
    private void Hunter(LightEntity e)
    {
        var prey=state.Entities.Where(p=>p.Alive&&p.Kind=="life"&&!Lit(p)&&!Sheltered(p)).OrderBy(p=>Distance(e.X,e.Y,p.X,p.Y)).FirstOrDefault();
        if(prey==null){e.Activity="waiting";return;}
        e.Activity="hunting";Walk(e,prey.X,prey.Y,.0027);
        if(!Lit(e)&&!Lit(prey)&&!Sheltered(prey)&&e.Cooldown==0&&Distance(e.X,e.Y,prey.X,prey.Y)<.035)
        {prey.Alive=false;e.Cooldown=180;Event("taken",prey);}
    }
    private void Walk(LightEntity e,double x,double y,double amount)
    {
        double d=Distance(e.X,e.Y,x,y);if(d<=.002)return;
        double scale=Math.Min(1,amount/d);double nx=e.X+(x-e.X)*scale,ny=e.Y+(y-e.Y)*scale;
        // Stop on the light boundary; entering light cannot grant one last action.
        if(state.LightOn&&Distance(nx,ny,state.LightX,state.LightY)<=Radius)return;
        e.X=nx;e.Y=ny;
    }
    private void Remember()
    {
        foreach(var entity in state.Entities.Where(Lit))
        {
            var old=state.Memories.FirstOrDefault(m=>m.Entity.Id==entity.Id);
            if(old==null){old=new(){Entity=new(){Id=entity.Id}};state.Memories.Add(old);}
            old.Entity=JsonSerializer.Deserialize<LightEntity>(JsonSerializer.Serialize(entity))!;old.SeenAt=state.Tick;
        }
        // A remembered silhouette disappears only after its old location is searched again.
        state.Memories.RemoveAll(m=>state.LightOn&&Distance(m.Entity.X,m.Entity.Y,state.LightX,state.LightY)<=Radius&&!state.Entities.Any(e=>e.Alive&&e.Id==m.Entity.Id&&Lit(e)));
    }
    private void Event(string key,LightEntity e)
    {
        state.Events.Add(new(state.Tick,key,e.X,e.Y));if(state.Events.Count>100)state.Events.RemoveAt(0);
        DecisionRecorded?.Invoke(new(state.Tick,"first_light",key,"darkness_and_local_conditions",new{e.Id,e.X,e.Y,e.Meals,e.Hunger}));
    }
}
