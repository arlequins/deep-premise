using System.Text.Json;

namespace DeepPremise.Core.Habits;

public sealed class Settler
{
    public int Id { get; set; }
    public string Name { get; set; } = "Neri";
    public int Job { get; set; }
    public int NextJob { get; set; } = -1;
    public double? ReturnX { get; set; }
    public double? ReturnY { get; set; }
    public bool CompletedWork { get; set; }
    public string Phase { get; set; } = "out";
    public int Progress { get; set; }
    public int Cargo { get; set; }
    public string Carrying { get; set; } = "food";
    public string Thought { get; set; } = "walking";
    public Dictionary<int,int> Visits { get; set; } = new();
    public List<string> Knowledge { get; set; } = new();
}
public sealed class HabitSite
{
    public int Id { get; set; }
    public string Name { get; set; } = "orchard";
    public string Resource { get; set; } = "food";
    public double X { get; set; }
    public double Y { get; set; }
    public int Reserve { get; set; } = 30;
    public bool Together { get; set; } = true;
    public bool Practice { get; set; }
    public int PairMemory { get; set; }
    public bool Bound { get; set; }
    public int Maker { get; set; } = -1;
    public int MakerMemory { get; set; }
    public bool Claimed { get; set; }
    public int EchoOwner { get; set; } = -1;
    public int EchoTrips { get; set; }
    public int EchoProgress { get; set; }
    public int EchoCargo { get; set; }
    public bool PairSeen { get; set; }
    public bool EchoSeen { get; set; }
    public bool ClaimSeen { get; set; }
    public long NextSoloTick { get; set; }
}
public sealed record FieldEvent(long Tick,string Key,string Person="",string Place="");
public sealed class HabitState
{
    public int Version { get; set; } = 8;
    public uint Seed { get; set; } = 1;
    public long Tick { get; set; }
    public int Day { get; set; } = 1;
    public Dictionary<string,int> Stock { get; set; } = new() { ["food"]=9,["water"]=8,["fiber"]=3,["cloth"]=0 };
    public int Strain { get; set; }
    public int Arrivals { get; set; }
    public bool GuestWaiting { get; set; }
    public bool Established { get; set; }
    public bool Failed { get; set; }
    public List<Settler> People { get; set; } = new();
    public List<HabitSite> Sites { get; set; } = new();
    public List<FieldEvent> Journal { get; set; } = new();
}

// Repetition leaves local material expectations. People can observe, exploit and rehearse alternatives.
public sealed class HabitWorld : IRecordedWorld
{
    private HabitState s;
    public HabitWorld(uint seed=17)
    {
        s=new(){Seed=seed};
        s.Sites=[new(){Id=0,Name="orchard",Resource="food",X=.23,Y=.27},new(){Id=1,Name="reedbank",Resource="fiber",X=.78,Y=.3},
            new(){Id=2,Name="spring",Resource="water",X=.73,Y=.77},new(){Id=3,Name="loom",Resource="cloth",X=.25,Y=.78}];
        var names=new[]{"Neri","Iven","Sela","Orren","Tavi"};var jobs=new[]{0,0,1,2,3};
        for(int i=0;i<5;i++)s.People.Add(new(){Id=i,Name=names[i],Job=jobs[i]});
        Note("arrival");
    }
    private HabitWorld(HabitState state){s=state;}
    public string RecordingKind=>"habits-v8";
    public long Tick=>s.Tick;
    public Action<SimulationDecision>? DecisionRecorded {get;set;}
    public Action? TickCompleted {get;set;}
    public HabitState Observe()=>JsonSerializer.Deserialize<HabitState>(SaveJson())!;
    public string SaveJson()=>JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true});
    public static HabitWorld LoadJson(string json)
    {
        var state=JsonSerializer.Deserialize<HabitState>(json)??throw new InvalidDataException("Empty world");
        if(state.Version!=8||state.Tick<0||state.People==null||state.Sites==null||state.Stock==null||state.Sites.Count!=4||
            state.Sites.Select(x=>x.Id).SequenceEqual(new[]{0,1,2,3})==false||
            state.People.Any(p=>p.Job is <0 or >3)||state.Stock.Values.Any(v=>v<0))throw new InvalidDataException("Invalid habit world");
        return new(state);
    }
    private void Trace(string key,object? facts=null)=>DecisionRecorded?.Invoke(new(s.Tick,"habits",key,"Local repetition, witnessed action and material state",facts));
    private void Note(string key,string person="",string place="")
    {
        s.Journal.Add(new(s.Tick,key,person,place));if(s.Journal.Count>24)s.Journal.RemoveAt(0);Trace(key,new{person,place});
    }
    public bool Assign(int person,int site)
    {
        var p=s.People.FirstOrDefault(p=>p.Id==person);
        if(p==null||site is <0 or >3||s.Failed||p.Job==site||p.NextJob==site)return false;
        var old=s.Sites[p.Job];
        if(p.Visits.GetValueOrDefault(old.Id)>=3+(int)(s.Seed%2)&&old.Id!=3&&old.EchoTrips==0)
        {
            old.EchoOwner=p.Id;old.EchoTrips=5;old.EchoProgress=0;old.EchoSeen=true;
            Note("echo_started",p.Name,old.Name);
        }
        // Finish the journey physically before taking a new route. Carried production is never teleported.
        double t=Math.Min(1,p.Progress/30.0);
        double x=p.Phase=="out"?.5+(old.X-.5)*t:p.Phase=="home"?(p.ReturnX??old.X)+(.5-(p.ReturnX??old.X))*t:p.Phase=="rest"?.5:old.X;
        double y=p.Phase=="out"?.52+(old.Y-.52)*t:p.Phase=="home"?(p.ReturnY??old.Y)+(.52-(p.ReturnY??old.Y))*t:p.Phase=="rest"?.52:old.Y;
        p.ReturnX=x;p.ReturnY=y;p.NextJob=site;p.Phase="home";p.Progress=0;p.Thought="new_task";Trace("assign",new{person,site});return true;
    }
    public bool ToggleTogether(int site)
    {
        if(site is <0 or >3||s.Failed)return false;
        s.Sites[site].Together=!s.Sites[site].Together;Trace("method",new{site,s.Sites[site].Together});return true;
    }
    public bool TogglePractice(int site)
    {
        if(site is <0 or >3||s.Failed)return false;
        s.Sites[site].Practice=!s.Sites[site].Practice;Note(s.Sites[site].Practice?"practice_started":"practice_stopped","",s.Sites[site].Name);return true;
    }
    public bool Admit()
    {
        if(!s.GuestWaiting||s.Stock["cloth"]<3||s.People.Count>=9||s.Failed)return false;
        s.Stock["cloth"]-=3;s.GuestWaiting=false;
        var name=new[]{"Mora","Edda","Bram","Lio"}[s.Arrivals++];
        s.People.Add(new(){Id=s.People.Count,Name=name,Job=1});Note("guest_joined",name);return true;
    }
    public void Run(int ticks){for(int i=0;i<ticks&&!s.Failed;i++)Step();}
    private void Step()
    {
        s.Tick++;
        if(s.Tick%30==0)foreach(var site in s.Sites.Where(x=>x.Id!=3))site.Reserve=Math.Min(30,site.Reserve+1);
        foreach(var p in s.People)
        {
            p.Progress++;
            if(p.Phase=="out"&&p.Progress>=30){p.Phase="work";p.Progress=0;p.Thought="working";}
            else if(p.Phase=="home"&&p.Progress>=30)
            {
                if(p.Cargo>0){s.Stock[p.Carrying]+=p.Cargo;Trace("delivered",new{p.Id,p.Job,p.Carrying,p.Cargo});}
                p.Cargo=0;if(p.CompletedWork)p.Visits[p.Job]=p.Visits.GetValueOrDefault(p.Job)+1;p.CompletedWork=false;
                if(p.NextJob>=0){p.Job=p.NextJob;p.NextJob=-1;}
                p.ReturnX=p.ReturnY=null;p.Phase="rest";p.Progress=0;p.Thought="resting";
            }
            else if(p.Phase=="rest"&&p.Progress>=12){p.Phase="out";p.Progress=0;p.Thought="walking";}
        }
        foreach(var site in s.Sites)
        {
            var ready=s.People.Where(p=>p.Job==site.Id&&p.Phase=="work"&&p.Progress>=45+(s.Strain>0?15:0)).ToArray();
            if(ready.Length>0)Work(site,ready);
            Echo(site);
            foreach(var p in s.People.Where(p=>p.Job==site.Id&&p.Phase=="work"))
            {
                foreach(var key in new[]{site.Bound?"pair":"",site.EchoTrips>0?"echo":"",site.Claimed?"maker":""}.Where(k=>k!=""))
                    if(!p.Knowledge.Contains(key)){p.Knowledge.Add(key);Trace("witnessed",new{Person=p.Id,key,Site=site.Id});}
            }
        }
        if(!s.GuestWaiting&&s.People.Count<9&&s.Day>=2+s.Arrivals&&s.Stock["cloth"]>=3){s.GuestWaiting=true;Note("guest_waiting");}
        if(s.Tick%300==0)
        {
            s.Day++;int need=s.People.Count;
            bool shortfall=s.Stock["food"]<need||s.Stock["water"]<need;
            s.Stock["food"]=Math.Max(0,s.Stock["food"]-need);s.Stock["water"]=Math.Max(0,s.Stock["water"]-need);
            s.Strain=shortfall?s.Strain+1:Math.Max(0,s.Strain-1);Note(shortfall?"hungry_day":"shared_meal");
            if(s.Day%3==0){s.Stock["cloth"]=Math.Max(0,s.Stock["cloth"]-1);Note("rain_day");}
            if(s.Strain>=4){s.Failed=true;Note("dispersed");}
            if(!s.Established&&s.Day>=6&&s.People.Count>=7&&s.Strain==0){s.Established=true;Note("established");}
        }
        TickCompleted?.Invoke();
    }
    private void Work(HabitSite site,Settler[] workers)
    {
        if(site.Practice)
        {
            site.PairMemory=Math.Max(0,site.PairMemory-1);site.MakerMemory=Math.Max(0,site.MakerMemory-1);
            if(site.Bound&&site.PairMemory==0){site.Bound=false;Note("pair_faded","",site.Name);}
            if(site.Claimed&&site.MakerMemory==0){site.Claimed=false;site.Maker=-1;Note("maker_faded","",site.Name);}
            foreach(var p in workers)Return(p,0,"rehearsing");return;
        }
        if(site.Id==3)
        {
            if(site.Claimed&&!workers.Any(p=>p.Id==site.Maker))
            {foreach(var p in workers)p.Thought="wrong_hands";return;}
            foreach(var p in workers)
            {
                if(s.Stock["fiber"]<2){p.Thought="need_fiber";continue;}
                s.Stock["fiber"]-=2;Return(p,site.Claimed?2:1,"carrying");
                if(site.Maker==p.Id)site.MakerMemory=Math.Min(3,site.MakerMemory+1);else if(!site.Claimed){site.Maker=p.Id;site.MakerMemory=1;}
                if(!site.Claimed&&site.MakerMemory>=3)
                {site.Claimed=true;site.ClaimSeen=true;Learn(p,"maker");Note("maker_bound",p.Name,site.Name);}
            }
            return;
        }
        if(site.Bound&&workers.Length<2){workers[0].Thought="too_heavy";return;}
        if(!site.Bound&&site.Together&&workers.Length<2&&s.People.Count(p=>p.Job==site.Id&&p.NextJob<0)>=2){workers[0].Thought="waiting_partner";return;}
        if(!site.Bound&&!site.Together)
        {
            if(s.Tick<site.NextSoloTick)return;
            workers=workers.Take(1).ToArray();site.NextSoloTick=s.Tick+10;
        }
        if(site.Together&&workers.Length>=2)
        {
            site.PairMemory=Math.Min(4,site.PairMemory+1);
            if(!site.Bound&&site.PairMemory>=3+(int)(s.Seed%2)){site.Bound=true;site.PairSeen=true;foreach(var p in workers)Learn(p,"pair");Note("pair_bound",workers[0].Name,site.Name);}
        }
        else if(!site.Bound)site.PairMemory=Math.Max(0,site.PairMemory-1);
        foreach(var p in workers)
        {
            int take=Math.Min(site.Reserve,site.Bound?3:2);site.Reserve-=take;
            Return(p,take,take>0?"carrying":"empty_site");
        }
    }
    private void Learn(Settler p,string knowledge){if(!p.Knowledge.Contains(knowledge)){p.Knowledge.Add(knowledge);Trace("witnessed",new{p.Id,knowledge});}}
    private void Return(Settler p,int cargo,string thought){p.Cargo=cargo;p.CompletedWork=true;p.ReturnX=p.ReturnY=null;p.Carrying=s.Sites[p.Job].Resource;p.Phase="home";p.Progress=0;p.Thought=thought;}
    private void Echo(HabitSite site)
    {
        if(site.EchoTrips<=0)return;
        site.EchoProgress++;
        if(site.EchoProgress==30&&site.Reserve>0){site.Reserve--;site.EchoCargo=1;}
        if(site.EchoProgress>=60)
        {
            s.Stock[site.Resource]+=site.EchoCargo;site.EchoCargo=0;site.EchoProgress=0;site.EchoTrips--;
            foreach(var p in s.People.Where(p=>p.Phase=="rest"))Learn(p,"echo");
            Trace("echo_delivered",new{site.Id,site.EchoOwner});
            if(site.EchoTrips==0)Note("echo_faded","",site.Name);
        }
    }
}
