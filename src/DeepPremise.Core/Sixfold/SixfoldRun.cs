using System.Text.Json;

namespace DeepPremise.Core.Sixfold;

public sealed record OrganSpec(string Id,int Interval,int Power,string Color);
public sealed class Organ
{
    public string Kind { get; set; } = "jaw";
    public int Level { get; set; } = 1;
    public int Clock { get; set; }
    public int Activations { get; set; }
    public bool Adapted { get; set; }
}
public sealed class Encounter
{
    public string Kind { get; set; } = "brute";
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Interval { get; set; }
    public int Clock { get; set; }
    public int Armor { get; set; }
    public int Poison { get; set; }
    public bool Elite { get; set; }
    public string Trait { get; set; } = "none";
}
public sealed record CombatEvent(long Tick,string Kind,int Amount,int Slot=-1);
public sealed class SixfoldState
{
    public int Version { get; set; } = 13;
    public uint Seed { get; set; }
    public uint Random { get; set; }
    public long Tick { get; set; }
    public string Phase { get; set; } = "starter";
    public int Floor { get; set; } = 1;
    public int Health { get; set; } = 65;
    public int MaxHealth { get; set; } = 65;
    public int Shield { get; set; }
    public int Poison { get; set; }
    public int Gold { get; set; } = 8;
    public int Wins { get; set; }
    public int Bosses { get; set; }
    public int Rerolls { get; set; }
    public int BattleTicks { get; set; }
    public int Drones { get; set; }
    public List<string> Relics { get; set; } = new();
    public string Route { get; set; } = "safe";
    public List<Organ?> Body { get; set; } = new() {null,null,null,null,null,null};
    public List<string> Offers { get; set; } = new();
    public List<string> RelicOffers { get; set; } = new();
    public Encounter Enemy { get; set; } = new();
    public List<CombatEvent> Events { get; set; } = new();
    public List<string> History { get; set; } = new();
}

// A bounded roguelike run: 12 encounters, three bosses, six slots, seeded drafts.
public sealed class SixfoldRun : IRecordedWorld
{
    public static readonly OrganSpec[] Catalog = [
        new("jaw",12,8,"edb17b"),new("stomach",24,5,"9ace98"),new("venom",22,3,"b1a0d8"),
        new("shell",23,8,"84b8d5"),new("spike",17,6,"dd8e8e"),new("heart",0,0,"df7397"),
        new("lung",21,5,"90d4c7"),new("eye",0,0,"e3d491"),new("brood",35,1,"a8b984"),
        new("lance",28,13,"e6d4b1"),new("leechfang",19,5,"e18aa7"),new("catalyst",30,4,"c194e0"),
        new("capacitor",32,5,"8ca9ef"),new("frost",25,4,"a0dcf0"),new("cannon",40,24,"e6a36b")];
    public static readonly string[] Relics=["fang","crown","blood","mirror","nursery","antidote"];
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private SixfoldState state=new();
    public string RecordingKind=>"sixfold-v13";
    public long Tick=>state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public SixfoldRun(uint seed=101){state.Seed=seed;state.Random=seed==0?1:seed;CreateEnemy();}
    public SixfoldState Observe()=>JsonSerializer.Deserialize<SixfoldState>(SaveJson())!;
    public string SaveJson()=>JsonSerializer.Serialize(state,Json);
    public static SixfoldRun LoadJson(string json)
    {
        var s=JsonSerializer.Deserialize<SixfoldState>(json)??throw new InvalidDataException("Missing run.");
        if(s.Version!=13||s.Body.Count!=6||s.Floor<1||s.Floor>12||s.Body.Any(o=>o!=null&&(!Catalog.Any(c=>c.Id==o.Kind)||o.Level<1||o.Level>3)))throw new InvalidDataException("Invalid run.");
        return new SixfoldRun(s.Seed){state=s};
    }
    private int Next(int max){state.Random^=state.Random<<13;state.Random^=state.Random>>17;state.Random^=state.Random<<5;return (int)(state.Random%(uint)max);}
    public static IEnumerable<int> Neighbors(int slot)
    {if(slot%3>0)yield return slot-1;if(slot%3<2)yield return slot+1;yield return slot<3?slot+3:slot-3;}
    public bool ChooseStarter(int index)
    {
        if(state.Phase!="starter"||index<0||index>2)return false;
        string[][] sets=[["jaw","heart"],["venom","stomach"],["shell","spike"]];
        state.Body[0]=new(){Kind=sets[index][0]};state.Body[1]=new(){Kind=sets[index][1]};
        state.Phase="draft";Draft();Trace("starter",new{index});return true;
    }
    private void Draft()
    {
        state.Offers=Catalog.Select(c=>c.Id).OrderBy(_=>Next(100000)).Take(3).ToList();
        // Every choice contains at least one immediately usable active organ.
        if(state.Offers.All(k=>k is "heart" or "eye"))state.Offers[0]="jaw";
        state.Rerolls=0;
    }
    public bool Take(int offer,int slot)
    {
        if(state.Phase!="draft"||offer<0||offer>=state.Offers.Count||slot<0||slot>=6)return false;
        string kind=state.Offers[offer];var current=state.Body[slot];
        if(current?.Kind==kind&&current.Level==3)return false;
        if(kind is "heart" or "eye" or "shell" or "lung" or "stomach" && !state.Body.Where((o,i)=>i!=slot).Any(o=>o!=null&&o.Kind is not ("heart" or "eye" or "shell" or "lung" or "stomach")))return false;
        if(current?.Kind==kind&&current.Level<3)current.Level++;
        else state.Body[slot]=new(){Kind=kind,Level=current?.Level??1,Adapted=current!=null};
        state.Phase="prepare";state.Offers.Clear();Trace("draft",new{kind,slot,Level=state.Body[slot]!.Level});return true;
    }
    public bool Reroll()
    {
        if(state.Phase!="draft"||state.Gold<4+state.Rerolls*2)return false;
        int rolls=state.Rerolls+1;state.Gold-=4+state.Rerolls*2;Draft();state.Rerolls=rolls;Trace("reroll",new{state.Rerolls});return true;
    }
    public bool Swap(int a,int b)
    {
        if(state.Phase is not ("draft" or "prepare")||a<0||a>=6||b<0||b>=6)return false;
        (state.Body[a],state.Body[b])=(state.Body[b],state.Body[a]);Trace("swap",new{a,b});return true;
    }
    public bool Upgrade(int slot)
    {
        if(state.Phase!="prepare"||slot<0||slot>=6||state.Body[slot] is not {}o||o.Level>=3||state.Gold<7+o.Level*3)return false;
        state.Gold-=7+o.Level*3;o.Level++;Trace("upgrade",new{slot,o.Level});return true;
    }
    public bool Rest()
    {
        if(state.Phase!="prepare"||state.Gold<7||state.Health>=state.MaxHealth)return false;
        state.Gold-=7;state.Health=Math.Min(state.MaxHealth,state.Health+22);Trace("rest",new{state.Health});return true;
    }
    public bool SetRoute(string route)
    {
        if(state.Phase!="prepare"||state.Floor%4==0||route is not ("safe" or "elite"))return false;
        state.Route=route;CreateEnemy();Trace("route",new{route});return true;
    }
    private void CreateEnemy()
    {
        string[] types=["brute","swarm","shellback","leech","toxic"];
        string kind=state.Floor%4==0?"boss"+state.Floor/4:types[((int)(state.Seed%5)+state.Floor-1)%types.Length];
        int tier=(state.Floor-1)/4;bool boss=state.Floor%4==0,elite=state.Route=="elite"&&!boss;
        int hp=34+state.Floor*9+Math.Max(0,state.Floor-3)*18+(boss?26+state.Floor*3:0)+(elite?23:0);
        state.Enemy=new(){Kind=kind,MaxHealth=hp,Health=hp,Attack=5+state.Floor+Math.Max(0,state.Floor-3)*3+(boss?3:0)+(elite?2:0),Interval=kind=="swarm"?13:kind=="brute"?27:22,Armor=kind=="shellback"?3+tier*2:kind=="boss2"?7:0,Elite=elite};
        if(state.Floor>=5)
        {
            string[] traits=["plated","purifying","regenerating","volatile"];
            state.Enemy.Trait=traits[(int)((state.Seed*2654435761u+(uint)state.Floor*2246822519u)%4)];
            if(state.Enemy.Trait=="plated")state.Enemy.Armor+=5;
        }
    }
    public bool Fight()
    {
        if(state.Phase!="prepare"||!state.Body.Any(o=>o!=null&&o.Kind is not ("heart" or "eye")))return false;
        state.Phase="battle";state.Shield=state.Relics.Contains("mirror")?15:0;state.Poison=0;state.Drones=0;state.BattleTicks=0;
        foreach(var o in state.Body.Where(o=>o!=null)){o!.Clock=0;o.Activations=0;}
        state.Events.Clear();Trace("battle_start",new{state.Floor,state.Route,Enemy=state.Enemy});return true;
    }
    public bool Continue()
    {
        if(state.Phase=="victory")
        {
            if(state.Floor==12){state.Phase="won";return true;}
            if(state.Floor%4==0){state.Phase="relic";state.RelicOffers=Relics.Where(r=>!state.Relics.Contains(r)).OrderBy(_=>Next(10000)).Take(3).ToList();return true;}
            NextFloor();return true;
        }
        return false;
    }
    public bool ChooseRelic(int index)
    {
        if(state.Phase!="relic"||index<0||index>=state.RelicOffers.Count)return false;
        state.Relics.Add(state.RelicOffers[index]);Trace("relic",new{Chosen=state.RelicOffers[index]});NextFloor();return true;
    }
    private void NextFloor(){foreach(var o in state.Body)if(o!=null)o.Adapted=false;state.Floor++;state.Route="safe";CreateEnemy();state.Phase="draft";Draft();}
    public void Run(int ticks)
    {
        for(int i=0;i<ticks&&state.Phase=="battle";i++)
        {
            state.Tick++;state.BattleTicks++;
            for(int slot=0;slot<6&&state.Enemy.Health>0;slot++)
            {
                var o=state.Body[slot];if(o==null)continue;var spec=Catalog.First(c=>c.Id==o.Kind);if(spec.Interval==0)continue;
                int hearts=Neighbors(slot).Sum(n=>state.Body[n]?.Kind=="heart"?state.Body[n]!.Level:0);
                int interval=Math.Max(6,spec.Interval-hearts*2-(o.Adapted?3:0));o.Clock++;
                if(o.Clock<interval)continue;o.Clock=0;o.Activations++;
                int power=spec.Power+(o.Level-1)*3+hearts*2;
                bool crit=Neighbors(slot).Any(n=>state.Body[n]?.Kind=="eye")&&o.Activations%3==0;
                if(crit)power*=2;
                if(o.Kind is "jaw" or "spike")Hit(power+(state.Relics.Contains("fang")?3:0),slot,crit?"critical":"hit");
                if(o.Kind=="lance")Hit(power+state.Enemy.Armor,slot,"pierce");
                if(o.Kind=="leechfang"){Hit(power,slot,"hit");Heal(2+o.Level,slot);}
                if(o.Kind=="cannon")Hit(power,slot,crit?"critical":"hit");
                if(o.Kind=="catalyst")
                {
                    int consumed=Math.Min(state.Enemy.Poison,3+o.Level*2);state.Enemy.Poison-=consumed;
                    Hit(power+consumed*3,slot,"burst");
                }
                if(o.Kind=="capacitor")
                {
                    int consumed=Math.Min(state.Shield,4+o.Level*3);state.Shield-=consumed;
                    Hit(power+consumed*2,slot,"burst");
                }
                if(o.Kind=="frost"){Hit(power,slot,"hit");state.Enemy.Clock=Math.Max(-state.Enemy.Interval,state.Enemy.Clock-3-o.Level*2);Emit("slow",3+o.Level*2,slot);}
                if(o.Kind=="venom"){state.Enemy.Poison+=o.Level*2+1+(state.Relics.Contains("crown")?2:0);Emit("venom",o.Level*2+1,slot);}
                if(o.Kind=="stomach")
                {
                    Heal(power,slot);
                    foreach(int n in Neighbors(slot).Where(n=>state.Body[n]?.Kind=="venom")){state.Enemy.Poison+=state.Body[n]!.Level*2;Emit("link",state.Body[n]!.Level*2,n);}
                }
                if(o.Kind=="shell"){int gain=Math.Min(power,40-state.Shield);state.Shield+=gain;Emit("shield",gain,slot);}
                if(o.Kind=="lung"){state.Poison=Math.Max(0,state.Poison-o.Level*2);state.Shield=Math.Min(40,state.Shield+power);Emit("cleanse",power,slot);}
                if(o.Kind=="brood"){state.Drones=Math.Min(state.Relics.Contains("nursery")?8:5,state.Drones+o.Level);Emit("brood",o.Level,slot);}
            }
            if(state.BattleTicks%10==0&&state.Enemy.Health>0)
            {
                if(state.Enemy.Trait=="purifying")state.Enemy.Poison=Math.Max(0,state.Enemy.Poison-2);
                if(state.Enemy.Trait=="regenerating")state.Enemy.Health=Math.Min(state.Enemy.MaxHealth,state.Enemy.Health+4);
                if(state.Enemy.Poison>0){int damage=state.Enemy.Kind=="boss1"?(state.Enemy.Poison+1)/2:state.Enemy.Poison;state.Enemy.Health=Math.Max(0,state.Enemy.Health-damage);state.Enemy.Poison=Math.Max(0,state.Enemy.Poison-1);Emit("poison",damage);}
                if(state.Drones>0)Hit(state.Drones,-1,"drone");
                if(state.Poison>0){int damage=state.Relics.Contains("antidote")?Math.Max(0,state.Poison-2):state.Poison;state.Health=Math.Max(0,state.Health-damage);state.Poison=Math.Max(0,state.Poison-1);Emit("hurt",damage);}
            }
            if(state.Enemy.Health>0&&++state.Enemy.Clock>=state.Enemy.Interval)
            {
                state.Enemy.Clock=0;int rage=state.BattleTicks/180;int damage=state.Enemy.Attack+rage*4;
                if(state.Enemy.Trait=="volatile")damage+=state.BattleTicks/30;
                int block=Math.Min(state.Shield,damage);state.Shield-=block;state.Health=Math.Max(0,state.Health-(damage-block));Emit("hurt",damage-block);
                if(block>0)
                {
                    Emit("block",block);
                    for(int slot=0;slot<6;slot++)if(state.Body[slot]?.Kind=="spike"&&Neighbors(slot).Any(n=>state.Body[n]?.Kind=="shell"))Hit(3+state.Body[slot]!.Level*2,slot,"reflect");
                    if(state.Relics.Contains("mirror"))Hit(3,-1,"reflect");
                }
                if(state.Enemy.Kind is "toxic" or "boss3")state.Poison+=2;
                if(state.Enemy.Kind=="leech")state.Enemy.Health=Math.Min(state.Enemy.MaxHealth,state.Enemy.Health+5);
                if(state.Enemy.Kind=="boss1"&&state.BattleTicks%44==0)state.Enemy.Poison=Math.Max(0,state.Enemy.Poison-2);
            }
            if(state.Health<=0){state.Phase="lost";Trace("run_lost",new{state.Floor,state.BattleTicks});}
            else if(state.Enemy.Health<=0)
            {
                state.Phase="victory";state.Wins++;if(state.Floor%4==0)state.Bosses++;
                int reward=8+(state.Enemy.Elite?8:0)+(state.Floor%4==0?8:0);state.Gold+=reward;state.Health=Math.Min(state.MaxHealth,state.Health+5);
                state.History.Add($"{state.Floor}:{state.Enemy.Kind}:{state.BattleTicks}:{state.Health}");Trace("battle_won",new{state.Floor,reward,state.Health,state.BattleTicks});
            }
            if(state.BattleTicks>=600&&state.Phase=="battle"){state.Phase="lost";Emit("exhausted",0);Trace("exhausted",new{state.Floor});}
            TickCompleted?.Invoke();
        }
    }
    private void Hit(int power,int slot,string kind)
    {
        int damage=Math.Max(1,power-state.Enemy.Armor);state.Enemy.Health=Math.Max(0,state.Enemy.Health-damage);Emit(kind,damage,slot);
        if(state.Relics.Contains("blood")&&kind is "hit" or "critical")Heal(2,slot);
    }
    private void Heal(int amount,int slot){int healed=Math.Min(amount,state.MaxHealth-state.Health);state.Health+=healed;Emit("heal",healed,slot);}
    private void Emit(string kind,int amount,int slot=-1){state.Events.Add(new(state.Tick,kind,amount,slot));if(state.Events.Count>100)state.Events.RemoveAt(0);}
    private void Trace(string choice,object facts)=>DecisionRecorded?.Invoke(new(state.Tick,"sixfold",choice,"player_choice_and_combat_rules",facts));
}
