using System.Text.Json;

namespace DeepPremise.Core.Caravan;

public sealed class CaravanSimulation : IRecordedWorld
{
    private CaravanWorld state;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented=true };
    public long Tick=>state.Tick;
    public string RecordingKind=>"caravan-v5";
    public Action<SimulationDecision>? DecisionRecorded { get; set; }
    public Action? TickCompleted { get; set; }
    public static readonly IReadOnlyList<GearRecipe> Recipes=Array.AsReadOnly(new[]
    {
        new GearRecipe("packs","Larger packs",0,8,2,12,2,"Each carrier can bring back two more items."),
        new GearRecipe("wheels","Trail wheels",0,14,6,18,2,"Shorten every journey. Ore comes from the old cutting."),
        new GearRecipe("lantern","Survey lantern",0,6,4,14,1,"Reveal the outer paths and new sources of supplies."),
        new GearRecipe("caravan","Another place on the cart",12,12,4,20,3,"Prepare supplies and equipment for one more companion.")
    });
    public CaravanSimulation(uint seed=12345)
    {
        state=new(){RandomState=seed==0?1:seed};
        var data=new[]{("The caravan",.12f,.52f,"",0),("Windfall grove",.34f,.22f,"food",80),("Broken ferry",.35f,.77f,"wood",100),
            ("Old cutting",.59f,.58f,"metal",90),("High orchard",.66f,.19f,"food",140),("Driftwood shore",.69f,.85f,"wood",160),("Silent crossing",.88f,.47f,"metal",180)};
        for(var i=0;i<data.Length;i++){var d=data[i];state.Sites.Add(new(){Id=i,Name=d.Item1,X=d.Item2+(i==0?0:(Next(9)-4)*.007f),Y=d.Item3+(i==0?0:(Next(9)-4)*.007f),Resource=d.Item4,Supply=d.Item5,Known=i<3});}
        foreach(var name in new[]{"Mara","Orren","Neri"})state.Crew.Add(new(){Id=state.Crew.Count+1,Name=name,Trait=new[]{"steady","quick","strong"}[Next(3)]});
        state.Condition=new[]{"clear","rain","lean"}[Next(3)];if(state.Condition=="lean"){state.Stock["food"]=10;state.Stock["wood"]=5;}
        state.AnchorCargo=Next(2)==0?"food":"wood";
        var pool=new List<string>{"swift","deep","shared","night","patient"};for(var i=0;i<3;i++){var n=Next(pool.Count);state.Charters.Add(pool[n]);pool.RemoveAt(n);}
        Notice("Choose a companion, then a destination. They will repeat the round trip.","start");
    }
    private CaravanSimulation(CaravanWorld saved)=>state=saved;
    private int Next(int max){var before=state.RandomState;var x=before;x^=x<<13;x^=x>>17;x^=x<<5;state.RandomState=x;var result=(int)(x%(uint)max);Trace("random","draw","Seeded expedition variation.",new{Before=before,After=x,Maximum=max,Result=result});return result;}
    private void Trace(string system,string decision,string reason,object? facts=null)=>DecisionRecorded?.Invoke(new(Tick,system,decision,reason,facts));
    private void Notice(string text,string kind="ordinary",int site=0){state.Notices.Add(new(Tick,text,kind,site));if(state.Notices.Count>60)state.Notices.RemoveAt(0);Trace("event",kind,text,new{site});}
    private Waystation Site(int id)=>state.Sites.Single(s=>s.Id==id);
    private int Capacity(Carrier p)=>5+state.Gear["packs"]*2+(p.Trait=="strong"?2:0)+(state.Charter=="deep"?3:state.Charter=="swift"?-1:0);
    private RememberedRoad Road(int a,int b)
    {
        var low=Math.Min(a,b);var high=Math.Max(a,b);var old=state.Roads.FirstOrDefault(r=>r.A==low&&r.B==high);if(old!=null)return old;
        var x=Site(a).X-Site(b).X;var y=Site(a).Y-Site(b).Y;var road=new RememberedRoad{A=low,B=high,Base=Math.Max(5,(int)(Math.Sqrt(x*x+y*y)*38))};state.Roads.Add(road);return road;
    }
    private static int RoadCost(RememberedRoad r)=>Math.Max(4,r.Base-Math.Min(r.Base/2,r.Memory/2));
    public ActionResult SetRoute(int id,IEnumerable<int> stops)
    {
        if(state.Ended)return new(false,"This expedition has ended. Start a new one when ready.");
        var p=state.Crew.FirstOrDefault(p=>p.Id==id);var route=stops.ToList();
        if(p==null||route.Count is <1 or >3||route.Distinct().Count()!=route.Count||route.Any(s=>s==0||!state.Sites.Any(n=>n.Id==s&&n.Known)))return new(false,"Choose up to three different known destinations.");
        if(p.Phase is "travel" or "gather")return new(false,"Recall this companion before changing their route.");
        p.Route=route;p.Recalling=false;p.Stop=0;Trace("route","assigned","The player designated an automatic loop returning to the caravan.",new{id,Stops=route});
        return new(true,"Route assigned. The companion will keep bringing supplies back.");
    }
    public ActionResult Recall(int id)
    {
        var p=state.Crew.FirstOrDefault(p=>p.Id==id);if(p==null)return new(false,"Choose a companion.");
        p.Recalling=true;if(p.Phase is "idle" or "rest"){p.Route.Clear();p.Recalling=false;p.Phase="idle";}
        Trace("route","recall","Finish the current leg and return safely with any cargo.",new{id});return new(true,"Returning after the current leg. Cargo will be kept.");
    }
    public ActionResult Craft(string kind)
    {
        var recipe=Recipes.FirstOrDefault(r=>r.Id==kind);if(recipe==null||state.Ended)return new(false,"Choose equipment to prepare.");
        if(state.Craft!=null)return new(false,"Finish the current equipment first.");
        if(state.Gear[kind]>=recipe.Limit)return new(false,"This equipment is already complete.");
        if(state.Stock["food"]<recipe.Food||state.Stock["wood"]<recipe.Wood||state.Stock["metal"]<recipe.Metal)return new(false,"Bring back the listed supplies first.");
        state.Stock["food"]-=recipe.Food;state.Stock["wood"]-=recipe.Wood;state.Stock["metal"]-=recipe.Metal;
        state.Craft=new(){Kind=kind,Required=recipe.Work+(state.Charter=="shared"?4:state.Charter=="patient"?-4:0)};
        Trace("equipment","ordered","Supplies reserved; companions at the caravan do the work.",new{kind,recipe.Food,recipe.Wood,recipe.Metal});return new(true,"Equipment queued. Keep one companion at the caravan to work on it.");
    }
    public static string CharterName(string id)=>id switch{"swift"=>"Light and fast","deep"=>"Bring more home","shared"=>"Share every meal","night"=>"Walk through the mist",_=>"Make time for craft"};
    public static string CharterDescription(string id)=>id switch
    {"swift"=>"Travel faster, but carry one fewer item.","deep"=>"Carry three extra items, but rest longer between trips.","shared"=>"Food lasts longer, but equipment takes longer to make.","night"=>"Ignore mist delays, but use food more often.",_=>"Make equipment faster, but take longer on the road."};
    public ActionResult ChooseCharter(string id)
    {
        if(state.Delivered<20||state.Charter!=""||!state.Charters.Contains(id)||state.Ended)return new(false,"A direction can be chosen after twenty supplies arrive.");
        state.Charter=id;Notice(CharterName(id),"charter");return new(true,"This choice will shape the rest of the expedition.");
    }
    public void Run(int ticks){if(ticks is <0 or >1000000)throw new ArgumentOutOfRangeException(nameof(ticks));for(var i=0;i<ticks&&!state.Ended;i++)Step();}
    private void StartLeg(Carrier p,int destination)
    {
        p.From=p.At;p.To=destination;p.Progress=0;p.Phase="travel";var road=Road(p.From,p.To);
        var cost=RoadCost(road)+(state.Condition=="rain"&&state.Charter!="night"?3:0)+(state.Charter=="patient"?3:0);
        cost-=state.Gear["wheels"]*2+(p.Trait=="quick"?2:0)+(state.Charter=="swift"?3:0);p.Duration=Math.Max(3,cost);
        Trace("movement","depart","Current road, equipment, trait and conditions determine this leg's duration.",new{p.Id,p.From,p.To,p.Duration,road.Memory,Cargo=new Dictionary<string,int>(p.Bag)});
    }
    private void Step()
    {
        state.Tick++;
        var mealInterval=state.Charter=="shared"?140:state.Charter=="night"?80:100;
        if(Tick%mealInterval==0)foreach(var p in state.Crew){if(state.Stock["food"]>0){state.Stock["food"]--;p.Hunger=Math.Max(0,p.Hunger-2);}else p.Hunger=Math.Min(6,p.Hunger+1);Trace("needs","meal","Supplies are shared by the travelling community.",new{p.Id,p.Hunger,Food=state.Stock["food"]});}
        if(Tick%200==0)foreach(var s in state.Sites.Where(s=>s.Resource=="food"))s.Supply=Math.Min(160,s.Supply+8);
        if(Tick%120==0)foreach(var r in state.Roads.Where(r=>Tick-r.LastUsed>100))r.Memory=Math.Max(0,r.Memory-2);
        foreach(var p in state.Crew)
        {
            if(p.Phase=="idle")
            {
                if(p.Route.Count==0)continue;p.Departed=Tick;p.Stop=0;StartLeg(p,p.Route[0]);continue;
            }
            if(p.Phase=="rest"){if(++p.Progress>=p.Rest){p.Phase="idle";p.Progress=0;}continue;}
            if(p.Phase=="travel")
            {
                if(++p.Progress<p.Duration)continue;var road=Road(p.From,p.To);road.Traversals++;road.LastUsed=Tick;p.At=p.To;
                if(p.To==0){Deliver(p,road);continue;}
                if(p.Recalling){StartLeg(p,0);continue;}
                p.Phase="gather";p.Progress=0;p.Duration=4;continue;
            }
            if(p.Phase=="gather")
            {
                if(++p.Progress<p.Duration)continue;var s=Site(p.At);var free=Capacity(p)-p.Bag.Values.Sum();var portion=p.Stop==p.Route.Count-1?free:Math.Max(1,free/(p.Route.Count-p.Stop));var take=Math.Min(s.Supply,Math.Min(free,portion));
                if(take>0){s.Supply-=take;p.Bag[s.Resource]+=take;Trace("cargo","loaded","Remaining pack space and stops determine this load.",new{p.Id,Site=s.Id,Resource=s.Resource,Amount=take});}
                p.Stop++;StartLeg(p,p.Recalling||p.Stop>=p.Route.Count||p.Bag.Values.Sum()>=Capacity(p)?0:p.Route[p.Stop]);
            }
        }
        if(state.Craft is {} order)
        {
            var hands=state.Crew.Count(p=>p.At==0&&p.Phase is "idle" or "rest");
            order.Progress+=hands;
            if(order.Progress>=order.Required)
            {
                state.Gear[order.Kind]++;state.Craft=null;Notice(Recipes.Single(r=>r.Id==order.Kind).Name,"equipment");
                if(order.Kind=="lantern")foreach(var s in state.Sites.Where(s=>!s.Known)){s.Known=true;Notice(s.Name,"discovery",s.Id);}
                if(order.Kind=="caravan") {var names=new[]{"Mara","Orren","Neri","Tavi","Iven","Sela"};var name=names[state.Crew.Count];state.Crew.Add(new(){Id=state.Crew.Count+1,Name=name,Trait=new[]{"steady","quick","strong"}[Next(3)]});Notice($"{name} joined the caravan.","arrival");}
            }
        }
        if(state.Crew.All(p=>p.Hunger==6)){state.Ended=true;Notice("The supplies ran out. The expedition is over; its record remains.","ending");}
        TickCompleted?.Invoke();
    }
    private void Deliver(Carrier p,RememberedRoad road)
    {
        var amount=p.Bag.Values.Sum();foreach(var entry in p.Bag)state.Stock[entry.Key]=Math.Min(9999,state.Stock[entry.Key]+entry.Value);
        var witnesses=state.Crew.Where(a=>a.Id!=p.Id&&a.At==0&&a.Phase is "idle" or "rest").Select(a=>a.Id).ToArray();
        if(p.Bag[state.AnchorCargo]>0&&witnesses.Length>0)
        {
            var before=RoadCost(road);road.Memory=Math.Min(30,road.Memory+2);if(RoadCost(road)<before&&road.Memory>=6)Notice($"{p.Name}: the return path felt shorter this time.","curious",p.From);
            Trace("road","recognized","A matching cargo was received in another person's presence; the place retains the route.",new{p.Id,road.A,road.B,road.Memory,Witnesses=witnesses,Cargo=state.AnchorCargo});
        }
        state.Delivered+=amount;state.Journeys++;p.LastTrip=(int)(Tick-p.Departed);
        if(amount>0)Notice($"{p.Name}: +{p.Bag["food"]} food, +{p.Bag["wood"]} wood, +{p.Bag["metal"]} metal.","delivery");
        else Notice($"{p.Name} returned with an empty pack.","shortage");
        foreach(var key in p.Bag.Keys.ToArray())p.Bag[key]=0;
        if(state.Journeys>=3&&!Site(3).Known){Site(3).Known=true;Notice("The companions found the old cutting. Metal can be brought back from there.","discovery",3);}
        if(state.Delivered>=40&&!Site(4).Known){Site(4).Known=true;Notice("A path to the high orchard has been mapped.","discovery",4);}
        if(p.Recalling){p.Route.Clear();p.Recalling=false;}
        p.Phase="rest";p.Progress=0;p.Rest=state.Charter=="deep"?10:4;
    }
    public CaravanView Observe()
    {
        var stage=state.Journeys==0?0:state.Gear["packs"]==0?1:state.Gear["lantern"]==0?2:state.Crew.Count<4?3:state.Delivered<120?4:5;
        var goals=new[]{"Send a companion to the grove or ferry. Watch the first supplies arrive.","Bring back eight wood and prepare larger packs. Leave someone at the caravan to craft.","Explore the old cutting for metal, then prepare a survey lantern.","Prepare another place on the cart and welcome a fourth companion.","Connect more destinations and bring home 120 supplies.","The network is working. Try another route, companion or expedition."};
        return new(Tick,state.Condition,state.Charter,state.Delivered>=20&&state.Charter==""?state.Charters.ToArray():[],state.Ended,state.Delivered,state.Journeys,
            new Dictionary<string,int>(state.Stock),new Dictionary<string,int>(state.Gear),state.Sites.Select(s=>new SiteView(s.Id,s.Known?s.Name:"Unmapped",s.X,s.Y,s.Known?s.Resource:"",s.Known?s.Supply:0,s.Known)).ToArray(),
            state.Crew.Select(p=>new CarrierView(p.Id,p.Name,p.Trait,p.Phase,p.From,p.To,p.At,(float)p.Progress/Math.Max(1,p.Phase=="rest"?p.Rest:p.Duration),p.Route.ToArray(),new Dictionary<string,int>(p.Bag),Capacity(p),p.LastTrip,p.Hunger,p.Recalling)).ToArray(),
            state.Roads.Select(r=>new RoadView(r.A,r.B,RoadCost(r),r.Base,r.Traversals)).ToArray(),state.Notices.AsEnumerable().Reverse().ToArray(),state.Craft?.Kind??"",state.Craft?.Progress??0,state.Craft?.Required??0,goals[stage],stage);
    }
    public string SaveJson()=>JsonSerializer.Serialize(state,Json);
    public static CaravanSimulation LoadJson(string json)
    {
        try
        {
            if(json.Length>4000000)throw new InvalidDataException("Expedition save too large.");var s=JsonSerializer.Deserialize<CaravanWorld>(json)!;
            void Require(bool c){if(!c)throw new InvalidDataException("Invalid expedition save.");}
            Require(s.Version==5&&s.RandomState!=0&&s.Tick>=0&&s.Tick<1000000000&&s.Delivered>=0&&s.Journeys>=0);
            Require(s.Condition is "clear" or "rain" or "lean"&&s.AnchorCargo is "wood" or "food");
            Require(s.Stock.Count==3&&new[]{"food","wood","metal"}.All(s.Stock.ContainsKey)&&s.Stock.Values.All(v=>v is >=0 and <=9999));
            Require(s.Gear.Count==4&&Recipes.All(r=>s.Gear.ContainsKey(r.Id)&&s.Gear[r.Id]>=0&&s.Gear[r.Id]<=r.Limit));
            Require(s.Sites.Count==7&&s.Sites.Select(n=>n.Id).Order().SequenceEqual(Enumerable.Range(0,7))&&s.Sites.All(n=>n.X is >=0 and <=1&&n.Y is >=0 and <=1&&n.Supply>=0&&n.Supply<=200&&n.Name.Length<80));
            Require(s.Crew.Count is >=3 and <=6&&s.Crew.Select(p=>p.Id).Distinct().Count()==s.Crew.Count&&s.Crew.Count==3+s.Gear["caravan"]&&s.Notices.Count<=60&&s.Roads.Count<=21);
            Require(s.Charters.Count==3&&s.Charters.Distinct().Count()==3&&s.Charters.All(c=>c is "swift" or "deep" or "shared" or "night" or "patient")&&(s.Charter==""||s.Charters.Contains(s.Charter)));
            foreach(var p in s.Crew){Require(p.Id>0&&p.Name.Length is >0 and <40&&p.Trait is "steady" or "quick" or "strong"&&p.Hunger is >=0 and <=6&&p.Route.Count<=3&&p.Route.Distinct().Count()==p.Route.Count&&p.Route.All(id=>id>0&&id<7&&s.Sites[id].Known));Require(p.At is >=0 and <7&&p.From is >=0 and <7&&p.To is >=0 and <7&&p.Duration>0&&p.Progress>=0&&p.Phase is "idle" or "rest" or "travel" or "gather");Require(p.Bag.Count==3&&new[]{"food","wood","metal"}.All(p.Bag.ContainsKey)&&p.Bag.Values.All(v=>v>=0&&v<=20));}
            foreach(var r in s.Roads)Require(r.A>=0&&r.B<7&&r.A<r.B&&r.Base>0&&r.Memory is >=0 and <=30&&r.Traversals>=0&&r.LastUsed<=s.Tick);
            if(s.Craft is {} craft)Require(Recipes.Any(r=>r.Id==craft.Kind)&&craft.Progress>=0&&craft.Required>0&&craft.Progress<craft.Required);
            return new(s);
        }
        catch(Exception ex)when(ex is JsonException or NullReferenceException or InvalidOperationException or ArgumentException){throw new InvalidDataException("Invalid expedition save.",ex);}
    }
}
