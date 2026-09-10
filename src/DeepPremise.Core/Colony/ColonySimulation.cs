using System.Text.Json;

namespace DeepPremise.Core.Colony;

public sealed class ColonySimulation : IRecordedWorld
{
    public const int Width = 28, Height = 18;
    public string RecordingKind => "colony-v4";
    private ColonyWorld state;
    public long Tick => state.Tick;
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static readonly IReadOnlyList<BuildingDefinition> Definitions = Array.AsReadOnly(new[]
    {
        new BuildingDefinition("shelter","Shelter",2,2,18,8,0,24,"Beds for three people. A place to rest and room for a newcomer."),
        new BuildingDefinition("field","Growing plot",2,2,10,0,0,16,"A worker tends the soil and harvests eight food each cycle."),
        new BuildingDefinition("workbench","Workbench",2,2,24,12,0,30,"Turn three wood and two stone into two tools."),
        new BuildingDefinition("storehouse","Storehouse",2,2,30,18,0,32,"Increase each stock limit by eighty."),
        new BuildingDefinition("woodcamp","Woodcutting camp",2,2,20,6,4,28,"Workers process nearby trees into larger timber bundles."),
        new BuildingDefinition("quarry","Stone yard",2,2,24,0,4,30,"Workers extract six stone from nearby deposits."),
        new BuildingDefinition("camp","Campfire",1,1,0,0,0,1,"The first gathering place.")
    });
    public static BuildingDefinition Definition(string kind) => Definitions.Single(d => d.Id == kind);
    private int Capacity => (state.Perk=="pantry"?100:60) + state.Buildings.Count(b => b.Built && b.Kind == "storehouse") * 80;
    public ColonySimulation(uint seed = 271828)
    {
        state = new ColonyWorld { RandomState = seed == 0 ? 1u : seed };
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
        {
            state.Terrain.Add(x >= 25 ? "water" : Next(6) == 0 ? "earth" : "grass");
            if (x >= 24 || (x >= 9 && x <= 17 && y >= 5 && y <= 12)) continue;
            var roll = Next(11);
            if (roll < 3) state.Resources.Add(new ColonyResource { Id = state.NextId++, X = x, Y = y,
                Kind = roll == 0 ? "stone" : roll == 1 ? "food" : "wood", Amount = roll == 0 ? 16 : roll == 1 ? 9 : 20 });
        }
        state.Buildings.Add(new ColonyBuilding { Id = state.NextId++, Kind = "camp", X = 13, Y = 9, Built = true, Work = 1 });
        foreach (var name in new[] { "Mara", "Orren", "Neri" }) state.Pawns.Add(new ColonyPawn { Id = state.NextId++, Name = name, X = 11 + state.Pawns.Count, Y = 10 });
        state.Landscape = new[]{"green","rocky","lean"}[Next(3)];
        if(state.Landscape=="rocky"){state.Stock["stone"]+=16;state.Stock["wood"]-=10;}
        if(state.Landscape=="lean"){state.Stock["food"]-=8;state.Stock["wood"]+=15;}
        var pool=new List<string>{"patient","swift","builder","pantry","wanderer"};
        for(var i=0;i<3;i++){var index=Next(pool.Count);state.PerkChoices.Add(pool[index]);pool.RemoveAt(index);}
        state.NextArrival = 300 + Next(120); state.ImprintThreshold = 2 + Next(2);
        Notice("Three people have arrived. Designate a shelter, then a growing plot.","arrival");
    }
    private ColonySimulation(ColonyWorld saved) => state = saved;
    private int Next(int maximum)
    {
        var before = state.RandomState; var x = before; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state.RandomState = x;
        var result = (int)(x % (uint)maximum);
        Trace("random","draw","Seeded world variation.",new { Before = before, After = x, Maximum = maximum, Result = result }); return result;
    }
    private void Trace(string system,string decision,string reason,object? facts = null) => DecisionRecorded?.Invoke(new(state.Tick,system,decision,reason,facts));
    private void Notice(string text,string kind = "ordinary",int x = 13,int y = 9)
    { state.Notices.Add(new(state.Tick,text,kind,x,y)); if (state.Notices.Count > 60) state.Notices.RemoveAt(0); Trace("event",kind,text,new { x,y }); }
    public bool Unlocked(string kind) => kind switch
    { "workbench" => state.Buildings.Any(b => b.Kind == "shelter" && b.Built), "woodcamp" or "quarry" => state.TotalTools >= 4, _ => true };
    public ActionResult CanPlace(string kind,int x,int y)
    {
        var d = Definitions.FirstOrDefault(d => d.Id == kind);
        if (d == null || kind == "camp") return new(false,"Choose a building.");
        if (!Unlocked(kind)) return new(false,kind == "workbench" ? "Finish a shelter first." : "Make four tools to unlock this building.");
        if (x < 0 || y < 0 || x + d.Width > Width || y + d.Height > Height) return new(false,"Keep the building inside the clearing.");
        for (var yy=y; yy<y+d.Height; yy++) for (var xx=x; xx<x+d.Width; xx++)
        {
            if (state.Terrain[yy*Width+xx] == "water") return new(false,"Choose dry ground.");
            if (state.Resources.Any(r => r.X==xx && r.Y==yy && r.Amount>0)) return new(false,"Gather these resources to clear the ground.");
            if (state.Buildings.Any(b => xx>=b.X && yy>=b.Y && xx<b.X+Definition(b.Kind).Width && yy<b.Y+Definition(b.Kind).Height)) return new(false,"Another building already uses this ground.");
        }
        if (state.Stock["wood"] < d.Wood || state.Stock["stone"] < d.Stone || state.Stock["tools"] < d.Tools) return new(false,"More materials are needed. Workers gather basic supplies automatically.");
        return new(true,"Click to place. Workers will build it.");
    }
    public ActionResult Place(string kind,int x,int y)
    {
        var result=CanPlace(kind,x,y); if (!result.Success) return result;
        var d=Definition(kind); state.Stock["wood"]-=d.Wood; state.Stock["stone"]-=d.Stone; state.Stock["tools"]-=d.Tools;
        var b=new ColonyBuilding { Id=state.NextId++,Kind=kind,X=x,Y=y }; state.Buildings.Add(b);
        Trace("construction","designated","Materials reserved when the player places a blueprint.",new { b.Id,kind,x,y,d.Wood,d.Stone,d.Tools });
        return new(true,"Blueprint placed. Watch the workers bring it to life.");
    }
    public ActionResult Gather(int id)
    {
        var r=state.Resources.FirstOrDefault(r=>r.Id==id && r.Amount>0); if(r==null)return new(false,"There is nothing left to gather here.");
        r.Designated=!r.Designated; Trace("work","designation","The player changed a gathering order.",new { id,r.Designated });
        return new(true,r.Designated ? "Gathering marked. A free worker will come here." : "Gathering order removed.");
    }
    public ActionResult SetPriority(int id,string priority)
    {
        var p=state.Pawns.FirstOrDefault(p=>p.Id==id); if(p==null || priority is not("auto" or "build" or "grow" or "craft" or "gather"))return new(false,"Choose a worker priority.");
        p.Priority=priority; p.Job=null; Trace("work","priority","The player changed this worker's preference.",new { id,priority }); return new(true,"Worker priority changed.");
    }
    public ActionResult ToggleProduction(int id)
    {
        var b=state.Buildings.FirstOrDefault(b=>b.Id==id && b.Built && b.Kind is "field" or "workbench" or "woodcamp" or "quarry");
        if(b==null)return new(false,"This building has no production cycle."); b.Producing=!b.Producing;
        foreach(var p in state.Pawns.Where(p=>p.Job?.Target==id))p.Job=null;
        Trace("production","toggle","The player controls whether this building runs.",new { id,b.Producing }); return new(true,b.Producing?"Production resumed.":"Production paused.");
    }
    public ActionResult Remove(int id)
    {
        var b=state.Buildings.FirstOrDefault(b=>b.Id==id && b.Kind!="camp"); if(b==null)return new(false,"The campfire stays for now.");
        var d=Definition(b.Kind); var divisor=b.Built?2:1; state.Stock["wood"]+=d.Wood/divisor; state.Stock["stone"]+=d.Stone/divisor; state.Stock["tools"]+=d.Tools/divisor;
        if(b.CyclePaid){ state.Stock["wood"]+=3; state.Stock["stone"]+=2; }
        state.Buildings.Remove(b); foreach(var p in state.Pawns.Where(p=>p.Job?.Target==id))p.Job=null;
        Trace("construction","removed","Unfinished materials are refunded; completed structures return half.",new { id,b.Built }); return new(true,"Materials returned to the stockpile.");
    }
    private void AddStock(string kind,int amount) => state.Stock[kind]=Math.Min(Capacity,state.Stock[kind]+amount);
    public void Run(int ticks)
    {
        if(ticks is <0 or >1000000)throw new ArgumentOutOfRangeException(nameof(ticks));
        for(var i=0;i<ticks;i++)Step();
    }
    private void Step()
    {
        if(state.Ended)return;
        state.Tick++;
        if(state.Tick%96==0) foreach(var p in state.Pawns)
        {
            if(state.Stock["food"] >= (Cold?2:1)){state.Stock["food"]-=Cold?2:1;p.Hunger=Math.Max(0,p.Hunger-2);}else p.Hunger=Math.Min(6,p.Hunger+1);
            Trace("needs","meal","Workers eat from the shared stock.",new { p.Id,p.Hunger,Food=state.Stock["food"] });
        }
        if(state.Tick%480==0)foreach(var r in state.Resources.Where(r=>r.Kind is "wood" or "food"))r.Amount=Math.Min(r.Kind=="wood"?20:9,r.Amount+3);
        foreach(var p in state.Pawns)
        {
            if(p.Job!=null && !ValidJob(p.Job)){Trace("work","released","The previous target is no longer available.",new { p.Id,p.Job });p.Job=null;}
            if(p.Job==null)p.Job=ChooseJob(p);
            if(p.Job==null){p.Energy=Math.Min(100,p.Energy+1);continue;}
            var job=p.Job;
            if(p.X!=job.X || p.Y!=job.Y){Move(p,job.X,job.Y);continue;}
            if(job.Kind=="rest") {p.Energy=Math.Min(100,p.Energy+5);if(p.Energy>=95)p.Job=null;continue;}
            if(state.Tick%3==0)p.Energy=Math.Max(0,p.Energy-(state.Perk=="wanderer"?2:1));
            if(job.Kind=="build")
            {
                var b=state.Buildings.Single(b=>b.Id==job.Target);b.Work+=(p.Name=="Orren"?2:1)+(state.Perk=="builder"?1:0);
                if(b.Work>=Definition(b.Kind).Work){b.Work=Definition(b.Kind).Work;b.Built=true;p.Job=null;Notice($"{p.Name} finished the {Definition(b.Kind).Name}.","built",b.X,b.Y);}
            }
            else if(job.Kind=="gather")
            {
                job.Progress++;if(job.Progress<job.Required)continue;
                var r=state.Resources.Single(r=>r.Id==job.Target);var amount=Math.Min(r.Amount,r.Kind=="food"?3:5);r.Amount-=amount;AddStock(r.Kind,amount);r.Designated=false;
                Trace("work","gathered","The worker completed a marked or needed supply job.",new { Pawn=p.Id,Node=r.Id,Resource=r.Kind,Amount=amount });p.Job=null;
            }
            else
            {
                var b=state.Buildings.Single(b=>b.Id==job.Target);AdvanceProduction(b,p.Name,false);if(b.Cycle==0)p.Job=null;
            }
        }
        AdvanceImprints();
        if(state.Tick>=state.NextArrival && state.Pawns.Count<6 && state.Buildings.Count(b=>b.Built&&b.Kind=="shelter")*3>state.Pawns.Count &&
            state.Buildings.Any(b=>b.Built&&b.Kind=="field") && state.Stock["food"]>=12)
        {
            var name=new[]{"Mara","Orren","Neri","Tavi","Iven","Sela"}[state.Pawns.Count];state.Pawns.Add(new ColonyPawn{Id=state.NextId++,Name=name,X=12,Y=10});state.Stock["food"]-=2;state.NextArrival=state.Tick+480;
            Notice($"{name} saw the smoke and decided to stay.","arrival");
        }
        if(state.Pawns.All(p=>p.Hunger>=6)){state.Ended=true;Notice("The camp could not feed its people. This expedition has ended.","ending");}
        TickCompleted?.Invoke();
    }
    private bool ValidJob(ColonyJob j) => j.Kind switch
    {
        "rest"=>true,"gather"=>state.Resources.Any(r=>r.Id==j.Target&&r.Amount>0),
        "build"=>state.Buildings.Any(b=>b.Id==j.Target&&!b.Built),
        _=>state.Buildings.Any(b=>b.Id==j.Target&&b.Built&&b.Producing&&CanProduce(b))
    };
    private bool CanProduce(ColonyBuilding b) => b.Kind switch
    {
        "field"=>state.Stock["food"]<=Capacity-8,
        "workbench"=>state.Stock["tools"]<Math.Min(Capacity,16) && (b.CyclePaid || state.Stock["wood"]>=3&&state.Stock["stone"]>=2),
        "woodcamp"=>state.Stock["wood"]<=Capacity-6&&NearbyResource(b,"wood")!=null,
        "quarry"=>state.Stock["stone"]<=Capacity-6&&NearbyResource(b,"stone")!=null,_=>false
    };
    private ColonyResource? NearbyResource(ColonyBuilding b,string kind) => state.Resources.Where(r=>r.Kind==kind&&r.Amount>0&&Math.Abs(r.X-b.X)+Math.Abs(r.Y-b.Y)<=10).OrderBy(r=>Math.Abs(r.X-b.X)+Math.Abs(r.Y-b.Y)).ThenBy(r=>r.Id).FirstOrDefault();
    private ColonyJob? ChooseJob(ColonyPawn p)
    {
        if(p.Energy<20){var bed=state.Buildings.FirstOrDefault(b=>b.Built&&b.Kind=="shelter")??state.Buildings.First(b=>b.Kind=="camp");return new(){Kind="rest",Target=bed.Id,X=bed.X,Y=bed.Y,Required=20};}
        var reserved=state.Pawns.Where(a=>a.Id!=p.Id&&a.Job!=null).Select(a=>a.Job!.Target).ToHashSet();
        var candidates=new List<(ColonyJob Job,int Rank)>();
        foreach(var b in state.Buildings.Where(b=>!reserved.Contains(b.Id)))
        {
            if(!b.Built)candidates.Add((new(){Kind="build",Target=b.Id,X=b.X,Y=b.Y,Required=Definition(b.Kind).Work},0));
            else if(b.Producing&&CanProduce(b))candidates.Add((new(){Kind=b.Kind=="field"?"grow":b.Kind=="workbench"?"craft":"gather-production",Target=b.Id,X=b.X,Y=b.Y,Required=CycleLength(b.Kind)},b.Kind=="field"?2:3));
        }
        foreach(var r in state.Resources.Where(r=>r.Amount>0&&!reserved.Contains(r.Id)))
        {
            var needed=r.Kind=="food"?state.Stock["food"]<state.Pawns.Count*4:r.Kind=="wood"?state.Stock["wood"]<32:state.Stock["stone"]<24;
            if(!r.Designated&&!needed)continue;if(state.Stock[r.Kind]>=Capacity)continue;
            candidates.Add((new(){Kind="gather",Target=r.Id,X=r.X,Y=r.Y,Required=r.Kind=="stone"?8:5},r.Designated?1:4));
        }
        var selected=candidates.OrderBy(c=>c.Rank-(p.Priority==c.Job.Kind?6:0)).ThenBy(c=>Math.Abs(c.Job.X-p.X)+Math.Abs(c.Job.Y-p.Y)).ThenBy(c=>c.Job.Target).FirstOrDefault();
        if(selected.Job!=null)Trace("work","assigned","Priority, available target, then walking distance choose work.",new { p.Id,p.Priority,Selected=selected.Job,Candidates=candidates.Select(c=>new { c.Job.Kind,c.Job.Target,c.Rank }).ToArray() });
        return selected.Job;
    }
    private void Move(ColonyPawn p,int targetX,int targetY)
    {
        var from=p.Y*Width+p.X;var target=targetY*Width+targetX;
        var queue=new Queue<int>();queue.Enqueue(from);var previous=new Dictionary<int,int>{{from,-1}};
        while(queue.Count>0&&!previous.ContainsKey(target))
        {
            var cell=queue.Dequeue();var x=cell%Width;var y=cell/Width;
            foreach(var (xx,yy) in new[]{(x+1,y),(x,y+1),(x-1,y),(x,y-1)})
            {
                if(xx<0||yy<0||xx>=Width||yy>=Height||state.Terrain[yy*Width+xx]=="water")continue;
                var next=yy*Width+xx;if(previous.TryAdd(next,cell))queue.Enqueue(next);
            }
        }
        if(!previous.ContainsKey(target)){p.Job=null;return;}
        var step=target;while(previous[step]!=from&&previous[step]!=-1)step=previous[step];p.X=step%Width;p.Y=step/Width;
        Trace("movement","step","Walk along a reachable dry path.",new { p.Id,From=from,To=step,Target=target });
    }
    private void AdvanceProduction(ColonyBuilding b,string worker,bool imprint)
    {
        if(b.Kind=="workbench"&&!b.CyclePaid){if(state.Stock["wood"]<3||state.Stock["stone"]<2)return;state.Stock["wood"]-=3;state.Stock["stone"]-=2;b.CyclePaid=true;}
        b.Cycle++;var required=CycleLength(b.Kind);if(b.Cycle<required)return;
        var resource=b.Kind=="field"?"food":b.Kind=="workbench"?"tools":b.Kind=="woodcamp"?"wood":"stone";
        var amount=b.Kind=="field"?8:b.Kind=="workbench"?2:6;
        if(state.Perk=="patient")amount+=amount/2;
        if(state.Perk=="swift")amount--;
        if(state.Perk=="pantry")amount+=resource=="food"?4:resource=="tools"?-1:0;
        if(b.Kind is "woodcamp" or "quarry") {var node=NearbyResource(b,resource);if(node==null){b.Cycle=0;return;}node.Amount=Math.Max(0,node.Amount-3);}
        AddStock(resource,amount);if(resource=="tools")state.TotalTools+=amount;b.Cycle=0;b.CyclePaid=false;b.CompletedCycles++;
        Trace("production","completed",imprint?"Repeated work persisted in a socially anchored place.":"A worker completed a paid production cycle.",new { b.Id,Worker=worker,Resource=resource,Amount=amount,Imprint=imprint });
        if(!imprint)Notice($"{worker}: +{amount} {resource}.","production",b.X,b.Y);
        else if(!b.NoticedImprint){b.NoticedImprint=true;Notice("Two tools were left on the workbench. Every worker was busy elsewhere.","curious",b.X,b.Y);}
    }
    private void AdvanceImprints()
    {
        if(state.Tick%2!=0)return;
        foreach(var b in state.Buildings.Where(b=>b.Built&&b.Kind=="workbench"&&b.Producing&&b.CompletedCycles>=state.ImprintThreshold&&CanProduce(b)))
        {
            if(state.Pawns.Any(p=>p.Job?.Target==b.Id))continue;
            if(state.Pawns.Count(p=>Math.Abs(p.X-b.X)+Math.Abs(p.Y-b.Y)<=5)<2)continue;
            AdvanceProduction(b,"place",true);
        }
    }
    public ColonyView Observe()
    {
        var goals=new[]{"Build a shelter for the first night.","Plant a growing plot for a steady food supply.","Build a workbench and start making tools.","Make four tools to open new ways of working.","Build a storehouse and a second shelter. Leave room to grow.","Keep the settlement working. Watch what changes when places are used."};
        var goal=!state.Buildings.Any(b=>b.Built&&b.Kind=="shelter")?0:!state.Buildings.Any(b=>b.Built&&b.Kind=="field")?1:!state.Buildings.Any(b=>b.Built&&b.Kind=="workbench")?2:state.TotalTools<4?3:!state.Buildings.Any(b=>b.Built&&b.Kind=="storehouse")||state.Buildings.Count(b=>b.Built&&b.Kind=="shelter")<2?4:5;
        return new(state.Tick,state.Terrain.ToArray(),new Dictionary<string,int>(state.Stock),Capacity,
            state.Pawns.Select(p=>new PawnView(p.Id,p.Name,p.X,p.Y,p.Energy,p.Hunger,p.Priority,p.Job?.Kind??"idle",
                p.Job==null?"":state.Buildings.FirstOrDefault(b=>b.Id==p.Job.Target) is {} site?Definition(site.Kind).Name:state.Resources.FirstOrDefault(r=>r.Id==p.Job.Target)?.Kind??"",
                p.Job?.Kind=="build"?state.Buildings.First(b=>b.Id==p.Job.Target).Work:p.Job?.Kind is "grow" or "craft" or "gather-production"?state.Buildings.First(b=>b.Id==p.Job.Target).Cycle:p.Job?.Progress??0,p.Job?.Required??0,p.Job?.X??p.X,p.Job?.Y??p.Y)).ToArray(),
            state.Buildings.Select(b=>new BuildingView(b.Id,b.Kind,b.X,b.Y,b.Built,b.Work,Definition(b.Kind).Work,b.Producing,b.Cycle,b.CompletedCycles)).ToArray(),
            state.Resources.Select(r=>new ResourceView(r.Id,r.Kind,r.X,r.Y,r.Amount,r.Designated)).ToArray(),state.Notices.AsEnumerable().Reverse().ToArray(),goals[goal],goal,state.TotalTools,state.Landscape,state.Perk,state.TotalTools>=2&&state.Perk==""?state.PerkChoices.ToArray():[],state.Ended,Cold);
    }
    public bool Cold => state.Tick/240%3==2;
    private int CycleLength(string kind)=>(kind=="field"?36:22)+(state.Perk=="patient"?10:state.Perk=="swift"?-6:state.Perk=="builder"?6:0);
    public static string PerkName(string id)=>id switch {"patient"=>"Patient craft","swift"=>"Quick turnover","builder"=>"Builders first","pantry"=>"A generous pantry",_=>"Open roads"};
    public static string PerkDescription(string id)=>id switch
    {
        "patient"=>"Larger harvests and batches, but every production cycle takes longer.",
        "swift"=>"Shorter production cycles, but one fewer item in each batch.",
        "builder"=>"Build faster, but production takes more time.",
        "pantry"=>"More food and storage, but fewer tools in each batch.",
        _=>"A newcomer can arrive sooner, but work tires everyone faster."
    };
    public ActionResult ChoosePerk(string id)
    {
        if(state.TotalTools<2||state.Perk!=""||!state.PerkChoices.Contains(id))return new(false,"This expedition has already chosen its direction.");
        state.Perk=id;if(id=="wanderer")state.NextArrival=Math.Max(state.Tick,state.NextArrival-120);
        foreach(var p in state.Pawns)p.Job=null;
        Notice(PerkName(id),"direction");Trace("expedition","direction","A permanent tradeoff for this expedition was selected.",new{id});return new(true,"A new direction for this expedition.");
    }
    public string SaveJson()=>JsonSerializer.Serialize(state,Json);
    public static ColonySimulation LoadJson(string json)
    {
        try
        {
            if(json.Length>4000000)throw new InvalidDataException("Colony save too large.");
            var s=JsonSerializer.Deserialize<ColonyWorld>(json)!;
            void Require(bool condition){if(!condition)throw new InvalidDataException("Invalid colony save.");}
            Require(s.Version==4&&s.RandomState!=0&&s.Tick>=0&&s.Tick<1000000000&&s.NextId>0&&s.ImprintThreshold is 2 or 3);
            Require(s.PerkChoices.Count==3&&s.PerkChoices.Distinct().Count()==3&&s.PerkChoices.All(p=>p is "patient" or "swift" or "builder" or "pantry" or "wanderer"));
            Require(s.Perk==""||s.PerkChoices.Contains(s.Perk));
            Require(s.Terrain.Count==Width*Height&&s.Terrain.All(t=>t is "water" or "earth" or "grass"));
            Require(s.Stock.Count==4&&new[]{"wood","stone","food","tools"}.All(s.Stock.ContainsKey)&&s.Stock.Values.All(v=>v>=0&&v<=100000));
            Require(s.Pawns.Count is >=3 and <=6&&s.Buildings.Count is >=1 and <=150&&s.Resources.Count<=Width*Height&&s.Notices.Count<=60);
            bool Cell(int x,int y)=>x>=0&&x<Width&&y>=0&&y<Height&&s.Terrain[y*Width+x]!="water";
            var ids=s.Pawns.Select(p=>p.Id).Concat(s.Buildings.Select(b=>b.Id)).Concat(s.Resources.Select(r=>r.Id)).ToArray();
            Require(ids.Distinct().Count()==ids.Length&&ids.All(id=>id>0&&id<s.NextId));
            Require(s.Buildings.Count(b=>b.Kind=="camp"&&b.Built)==1);
            foreach(var b in s.Buildings){Require(Definitions.Any(d=>d.Id==b.Kind)&&Cell(b.X,b.Y)&&b.Work>=0&&b.Work<=Definition(b.Kind).Work&&b.Cycle>=0&&b.Cycle<=52&&b.CompletedCycles>=0);}
            foreach(var r in s.Resources)Require(Cell(r.X,r.Y)&&r.Kind is "wood" or "stone" or "food"&&r.Amount>=0&&r.Amount<=20);
            foreach(var p in s.Pawns){Require(Cell(p.X,p.Y)&&p.Name.Length is >0 and <40&&p.Energy is >=0 and <=100&&p.Hunger is >=0 and <=6);if(p.Job is {} j)Require(Cell(j.X,j.Y)&&ids.Contains(j.Target)&&j.Progress>=0&&j.Required>0&&j.Kind is "rest" or "build" or "gather" or "grow" or "craft" or "gather-production");}
            return new(s);
        }
        catch(Exception ex) when(ex is JsonException or NullReferenceException or InvalidOperationException or ArgumentException){throw new InvalidDataException("Invalid colony save.",ex);}
    }
}
