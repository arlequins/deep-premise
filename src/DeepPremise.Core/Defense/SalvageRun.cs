using System.Text.Json;

namespace DeepPremise.Core.Defense;

public sealed class Rig { public int Cell { get; set; } public string Kind { get; set; } = "spark"; public int Level { get; set; } = 1; public int Cooldown { get; set; } public string Captured { get; set; } = "none"; public int Shield { get; set; } public int Satiation { get; set; } = 360; public int Growth { get; set; } = 1; }
public sealed class Intruder
{
    public int Id { get; set; } public int Lane { get; set; } public double X { get; set; } = 7.6;
    public string Kind { get; set; } = "runner"; public double Health { get; set; } public double MaxHealth { get; set; }
    public int Oil { get; set; } public int Burn { get; set; } public int Slow { get; set; }
    public int Flight { get; set; } public int FromLane { get; set; }
    public double FromX { get; set; } public double LandingX { get; set; }
    public bool Cracked { get; set; }
    public bool Escaped { get; set; }
    public List<int> Collided { get; set; } = new();
}
public sealed class Impact { public long Until { get; set; } public double X { get; set; } public int Lane { get; set; } public string Kind { get; set; } = "spark"; public double From { get; set; } public double Damage { get; set; } }
public sealed class RunState
{
    public int Version { get; set; } = 7; public uint Random { get; set; } = 1; public long Tick { get; set; }
    public int Wave { get; set; } = 1; public string Phase { get; set; } = "build"; public int Scrap { get; set; } = 16;
    public int Hull { get; set; } = 18; public int Kills { get; set; } public int Combo { get; set; } public int BestCombo { get; set; }
    public int Pending { get; set; } public int SpawnTimer { get; set; } public int NextId { get; set; }
    public int Pulse { get; set; } public int Power { get; set; } public int Range { get; set; } public int Salvage { get; set; }
    public string Weather { get; set; } = "dry"; public string Adaptation { get; set; } = "none";
    public int FireHits { get; set; } public int SparkHits { get; set; } public int ShiftHits { get; set; }
    public List<Rig> Rigs { get; set; } = new(); public List<Intruder> Enemies { get; set; } = new();
    public List<Impact> Impacts { get; set; } = new(); public List<string> Rewards { get; set; } = new();
    public List<string> History { get; set; } = new();
    public string Mission { get; set; } = "rescue";
    public int MissionLane { get; set; }
    public bool Dispatched { get; set; }
    public string CourierPhase { get; set; } = "idle";
    public double CourierX { get; set; }
    public int CourierHealth { get; set; } = 5;
    public bool Cargo { get; set; }
    public bool MissionComplete { get; set; }
    public List<string> Crew { get; set; } = new();
    public List<string> Relics { get; set; } = new();
    public string Relic { get; set; } = "none";
    public int Debt { get; set; }
    public int WaveTick { get; set; }
    public int CargoTier { get; set; }
    public int SlingCooldown { get; set; }
    public int HeldEnemy { get; set; } = -1;
    public int HoldTicks { get; set; }
    public bool AdventureOpening { get; set; }
    public bool ExpeditionPlanned { get; set; }
}

public sealed partial class SalvageRun : IRecordedWorld
{
    private RunState s;
    public SalvageRun(uint seed) { s = new() { Random = seed == 0 ? 1 : seed }; }
    public static SalvageRun Adventure(uint seed)
    {
        var run=new SalvageRun(seed);run.s.AdventureOpening=true;
        run.s.Rigs.Add(new(){Cell=2,Kind="spark"});run.s.Rigs.Add(new(){Cell=11,Kind="collector"});return run;
    }
    private SalvageRun(RunState state) { s = state; }
    public string RecordingKind => "salvage-v7"; public long Tick => s.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; } public Action? TickCompleted { get; set; }
    public RunState Observe() => JsonSerializer.Deserialize<RunState>(SaveJson())!;
    public string SaveJson() => JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
    public static SalvageRun LoadJson(string json)
    {
        var state = JsonSerializer.Deserialize<RunState>(json) ?? throw new InvalidDataException("Empty run");
        if (state.Version != 7 || state.Wave is < 1 or > 8 || state.Scrap < 0 || state.Rigs == null || state.Enemies == null ||
            state.Rigs.Any(r => r.Cell is < 0 or >= 21 || !Kinds.Contains(r.Kind) || r.Level is < 1 or > 3) ||
            state.Rigs.Select(r => r.Cell).Distinct().Count() != state.Rigs.Count || state.Enemies.Any(e => e.Lane is < 0 or > 2))
            throw new InvalidDataException("Invalid run");
        return new(state);
    }
    public static readonly string[] Kinds = ["spark", "oil", "flame", "shifter", "collector"];
    public static int Cost(string kind) => kind switch { "spark" => 4, "oil" => 3, "flame" => 5, "shifter" => 4, "collector" => 3, _ => 999 };
    private int Next(int max) { s.Random ^= s.Random << 13; s.Random ^= s.Random >> 17; s.Random ^= s.Random << 5; return (int)(s.Random % max); }
    private void Log(string key, object? facts = null)
    {
        s.History.Add(key); if (s.History.Count > 5) s.History.RemoveAt(0);
        DecisionRecorded?.Invoke(new(s.Tick,"salvage",key,"Resolved from equipment, enemy status, seeded encounter and player orders",facts));
    }
    public bool Place(int cell, string kind)
    {
        if (s.Phase != "build" || cell is < 0 or >= 21 || !Kinds.Contains(kind)) return false;
        var rig = s.Rigs.FirstOrDefault(r => r.Cell == cell);
        var cost = rig == null ? Cost(kind) : 3 + rig.Level * 2;
        if (s.Scrap < cost || rig?.Level >= 3) return false;
        s.Scrap -= cost;
        if (rig == null) s.Rigs.Add(new() { Cell = cell, Kind = kind }); else rig.Level++;
        Log(rig == null ? "placed" : "upgraded",new { cell,kind,cost }); return true;
    }
    public bool Sell(int cell)
    {
        var rig=s.Rigs.FirstOrDefault(r=>r.Cell==cell);
        if(s.Phase!="build" || rig==null) return false;
        s.Scrap += Cost(rig.Kind) + (rig.Level >= 2 ? 5 : 0) + (rig.Level >= 3 ? 7 : 0);
        s.Rigs.Remove(rig); Log("repacked",new { cell }); return true;
    }
    public bool Launch()
    {
        if(s.Phase!="build" || !s.Rigs.Any(r=>r.Kind is "spark" or "flame" or "shifter")) return false;
        s.Phase="fight"; s.Pending=6+s.Wave*3; s.SpawnTimer=8; s.Pulse=0; s.Combo=0;
        s.Dispatched=false;s.CourierPhase="idle";s.CourierX=0;s.CourierHealth=5;s.Cargo=false;s.MissionComplete=false;
        s.WaveTick=0;s.CargoTier=0;s.SlingCooldown=0;
        if(s.AdventureOpening&&s.Wave==1)
        {
            s.Enemies.Add(new(){Id=++s.NextId,Lane=0,X=4.8,Kind="sprinter",Health=16,MaxHealth=16,Oil=160});
            foreach(var x in new[]{5.0,5.7,6.4})s.Enemies.Add(new(){Id=++s.NextId,Lane=1,X=x,Health=7,MaxHealth=7});
            s.Pending-=4;
        }
        if(s.ExpeditionPlanned)Dispatch();
        s.FireHits=s.SparkHits=s.ShiftHits=0; Log("launched",new { s.Wave,s.Weather,s.Adaptation }); return true;
    }
    public bool Pulse(int lane)
    {
        if(s.Phase!="fight" || s.Pulse>0 || lane is <0 or >2) return false;
        s.Pulse=s.Crew.Contains("scout")?85:110;
        if(s.Relic=="debt") {s.Debt+=2;Log("debt_borrowed");}
        foreach(var e in s.Enemies.Where(e=>e.Lane==lane)) { e.X+=s.Relic=="inversion"?-.8:s.Relic=="debt"?1.8:.85; e.Slow=25; Hit(e,2,"pulse",0); }
        Log("pulse",new { lane }); return true;
    }
    public bool Choose(string reward)
    {
        if(s.Phase!="reward" || !s.Rewards.Contains(reward)) return false;
        switch(reward)
        {
            case "power": s.Power++; break;
            case "range": s.Range++; break;
            case "salvage": s.Salvage++; break;
            case "repair": s.Hull=Math.Min(18,s.Hull+7); s.Scrap+=4; break;
            case "cache": s.Scrap+=10; break;
        }
        s.Wave++; s.Phase="build"; s.Rewards.Clear(); s.Impacts.Clear();
        s.MissionLane=Next(3);
        s.Weather=new[]{"dry","rain","wind"}[Next(3)]; Log("reward_"+reward); return true;
    }
    private void Hit(Intruder e,double damage,string kind,double from)
    {
        if(kind=="spark" && s.Adaptation=="insulated") damage*=.65;
        if(kind=="flame" && s.Adaptation=="plated") damage*=.65;
        if(e.Kind=="armored") damage=Math.Max(.5,damage-1)*(e.Cracked?1:.45);
        if(s.Relic=="inversion" && e.X<2.5 && kind!="pulse")damage*=2;
        e.Health-=damage; s.Impacts.Add(new(){X=e.X,Lane=e.Lane,Kind=kind,From=from,Until=s.Tick+7,Damage=damage});
        DecisionRecorded?.Invoke(new(s.Tick,"combat","hit","Damage after enemy armor and learned resistance",new {e.Id,e.Lane,e.X,damage,kind,from,e.Health,s.Adaptation}));
    }
    public void Run(int ticks) { for(int i=0;i<ticks;i++) { if(s.Phase!="fight") break; Step(); } }
    private void Step()
    {
        s.Tick++; if(s.Pulse>0) s.Pulse--; s.Impacts.RemoveAll(i=>i.Until<s.Tick);
        LivingMachinesStep();
        ExpeditionStep();
        if(s.Pending>0 && --s.SpawnTimer<=0)
        {
            var kind=s.Wave>=3 && Next(4)==0 ? "armored" : Next(5)==0 ? "sprinter" : "runner";
            if(s.Wave%4==0 && s.Pending==1) kind="brute";
            double health=(5+s.Wave*1.9)*(kind=="brute" ? 4 : kind=="armored" ? 1.7 : kind=="sprinter" ? .65 : 1);
            if(s.AdventureOpening)health*=1+Math.Max(0,s.Wave-1)*.5;
            s.Enemies.Add(new(){Id=++s.NextId,Lane=Next(3),Kind=kind,Health=health,MaxHealth=health});
            DecisionRecorded?.Invoke(new(s.Tick,"combat","spawn","Seeded wave composition",s.Enemies[^1]));
            s.Pending--; s.SpawnTimer=Math.Max(7,27-s.Wave*(s.AdventureOpening?3:2))+Next(7);
        }
        foreach(var e in s.Enemies)
        {
            if(e.Health<=0) continue;
            if(e.Id==s.HeldEnemy)continue;
            if(e.Flight>0){FlightStep(e);continue;}
            var pace=e.Kind=="sprinter" ? .05 : e.Kind=="brute" ? .016 : .026;
            e.X-=pace*(e.Slow>0 || e.Oil>0 ? .55 : 1)*(s.Weather=="wind"?1.12:1);
            if(e.Oil>0)e.Oil--; if(e.Slow>0)e.Slow--;
            if(e.Burn>0) { e.Burn--; if(s.Tick%5==0) e.Health-=.65+s.Power*.2; }
        }
        foreach(var rig in s.Rigs)
        {
            if(rig.Cooldown>0) {rig.Cooldown--;continue;}
            int lane=rig.Cell/7; double x=rig.Cell%7+.5;
            if(rig.Kind=="collector")
            {
                rig.Cooldown=90;
                if(s.Pending>0){s.Scrap+=rig.Level;s.Impacts.Add(new(){X=x,Lane=lane,Kind="income",Until=s.Tick+8});}
                if(rig.Captured is "runner" or "brute")foreach(var e in s.Enemies.Where(e=>e.Health>0&&e.Id!=s.HeldEnemy&&e.Flight==0&&e.Lane==lane&&Math.Abs(e.X-x)<1.8))Hit(e,(rig.Captured=="brute"?8:2)*rig.Growth,"spark",x);
                continue;
            }
            var reach=rig.Kind=="oil" || rig.Kind=="shifter" ? 1.0 : 1.7+s.Range*.35;
            var target=s.Enemies.Where(e=>e.Health>0 && e.Id!=s.HeldEnemy && e.Flight==0 && e.Lane==lane && Math.Abs(e.X-x)<reach).OrderBy(e=>e.X).FirstOrDefault();
            if(target==null)continue;
            rig.Cooldown=(rig.Kind=="oil" ? 18 : rig.Kind=="shifter" ? 35 : 15)-(s.Crew.Contains("engineer")?2:0);
            var haste=s.Rigs.Where(r=>r.Captured=="sprinter"&&r.Cell/7==lane&&Math.Abs(r.Cell%7-rig.Cell%7)<=2).Select(r=>r.Growth).DefaultIfEmpty(0).Max();
            if(haste>0)rig.Cooldown=Math.Max(3,rig.Cooldown/(1+haste));
            var damage=(rig.Kind=="flame"?2.1:2.7)+(rig.Level-1)*1.7+s.Power;
            switch(rig.Kind)
            {
                case "oil": target.Oil=95; Hit(target,0,"oil",x); break;
                case "spark":
                    Hit(target,damage,"spark",x); s.SparkHits++;
                    if(s.Weather=="rain" || target.Oil>0)
                    {
                        foreach(var other in s.Enemies.Where(e=>e.Id!=target.Id && e.Health>0 && Math.Abs(e.X-target.X)<1.3 && Math.Abs(e.Lane-target.Lane)<=1).Take(3)) Hit(other,damage*.75,"spark",target.X);
                        if(s.Enemies.Count(e=>e.Health>0 && Math.Abs(e.X-target.X)<1.3)>1) { s.Combo++; Log("chain"); }
                    }
                    break;
                case "flame":
                    Hit(target,damage,"flame",x); target.Burn=30; s.FireHits++;
                    if(target.Oil>0)
                    {
                        target.Oil=0;
                        foreach(var other in s.Enemies.Where(e=>e.Health>0 && e.Lane==lane && Math.Abs(e.X-target.X)<1.5)) Hit(other,5+rig.Level,"blast",target.X);
                        s.Combo++; Log("ignition");
                    }
                    break;
                case "shifter":
                    Hit(target,damage,"shift",x); target.Lane=(target.Lane+1)%3; target.X+=.25; target.Slow=20;
                    if(s.Relic=="relay")
                    {
                        foreach(var other in s.Enemies.Where(e=>e.Id!=target.Id&&e.Lane==target.Lane&&Math.Abs(e.X-target.X)<1.6))
                        {other.Oil=Math.Max(other.Oil,target.Oil);other.Burn=Math.Max(other.Burn,target.Burn);Hit(other,3,"spark",target.X);}
                        Log("relay_triggered");
                    }
                    s.ShiftHits++; Log("redirected",new { target.Id,target.Lane }); break;
            }
        }
        foreach(var e in s.Enemies.Where(e=>e.Health<=0).ToArray())
        {
            if(s.HeldEnemy==e.Id){s.HeldEnemy=-1;s.HoldTicks=0;s.SlingCooldown=40;}
            s.Kills++; if(!e.Escaped)s.Scrap+=(s.AdventureOpening?(s.Kills%2==0?1:0):1)+s.Salvage; s.Impacts.Add(new(){X=e.X,Lane=e.Lane,Kind="kill",Until=s.Tick+8}); s.Enemies.Remove(e);
        }
        foreach(var e in s.Enemies.Where(e=>e.X<-.35).ToArray())
        {
            var shield=s.Rigs.FirstOrDefault(r=>r.Shield>0&&r.Cell/7==e.Lane);
            if(shield!=null){shield.Shield--;Log("graft_blocked");}else{s.Hull-=e.Kind=="brute"?4:1;Log("breach",new{e.Kind,e.Lane});}
            s.Enemies.Remove(e);
        }
        s.BestCombo=Math.Max(s.BestCombo,s.Combo);
        if(s.Hull<=0) { s.Hull=0;s.Phase="lost";Log("lost"); }
        else if(s.Pending==0 && s.Enemies.Count==0 && s.Debt==0 && s.CourierPhase is not ("outbound" or "returning"))
        {
            s.Scrap+=5; s.Phase=s.Wave==8 ? "won" : "reward";
            s.Adaptation=s.SparkHits>s.FireHits && s.SparkHits>8 ? "insulated" : s.FireHits>8 ? "plated" : "none";
            var pool=new List<string>{"power","range","salvage","repair","cache"};
            while(s.Rewards.Count<3) {int n=Next(pool.Count); s.Rewards.Add(pool[n]);pool.RemoveAt(n);}
            Log("wave_clear",new {s.Wave,s.Hull,s.Adaptation});
        }
        TickCompleted?.Invoke();
    }
}
