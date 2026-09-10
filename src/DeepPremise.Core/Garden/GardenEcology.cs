namespace DeepPremise.Core.Garden;

public sealed partial class GardenWorld
{
    public static readonly string[] Kinds = ["snail","stone","grass","seed","pond","beetle","moth","moss","moonstone","egg"];
    private void EnsureSupplies(){foreach(string kind in Kinds)state.Pouch.TryAdd(kind,0);}
    private int NextId()=>state.Objects.Concat(state.Stored).Select(o=>o.Id).DefaultIfEmpty(-1).Max()+1;
    private void Discover(string key)
    {
        if(state.Discoveries.Contains(key))return;
        state.Discoveries.Add(key);Trace("discovery",new{Key=key});
    }
    private void Supply(string kind,int amount)=>state.Pouch[kind]=Math.Min(24,state.Pouch[kind]+amount);
    public bool Store(int id)
    {
        var o=state.Objects.FirstOrDefault(o=>o.Id==id);
        if(o==null||o.Away||state.Pouch[o.Kind]>=24)return false;
        state.Objects.Remove(o);state.Stored.Add(o);state.Pouch[o.Kind]++;
        foreach(var other in state.Objects.Where(p=>p.Following==id))other.Following=-1;
        Trace("store",new{id,o.Kind,o.Hue,o.Age});return true;
    }
    public string ExpeditionRequirement()
    {
        if(state.ExpeditionTicks>0)return "exploring";
        if(!state.DiscoveredBloom)return "need_bloom";
        if(state.Trips==1&&!state.Discoveries.Contains("watering"))return "need_water";
        if(state.Trips==2&&(!state.Discoveries.Contains("pollination")||!state.Discoveries.Contains("flower_blue")))return "need_pollination";
        if(!state.Objects.Any(o=>o.Kind=="snail"&&o.Glow>=1&&o.Age>=600&&!o.Away))return "need_explorer";
        return "ready";
    }
    public bool StartExpedition()
    {
        string requirement=ExpeditionRequirement();
        if(requirement!="ready"){Trace("expedition_rejected",new{requirement});return false;}
        var explorer=state.Objects.Where(o=>o.Kind=="snail"&&o.Glow>=1&&o.Age>=600&&!o.Away).OrderBy(o=>o.Id).First();
        explorer.Away=true;explorer.Behavior="exploring";state.Explorer=explorer.Id;
        state.ExpeditionDuration=state.ExpeditionTicks=240+Math.Min(state.Trips,4)*120;
        Trace("expedition_started",new{state.Explorer,state.Trips,state.ExpeditionTicks});return true;
    }
    private void ExtendedBehavior(GardenObject o,GardenObject[] previous)
    {
        if(o.Kind=="pond"){o.Behavior="water_source";return;}
        if(o.Kind=="moss"){o.Behavior="soft_nest";return;}
        if(o.Kind=="moonstone"){o.Behavior=state.Night?"moonlit":"moon_sleep";o.Glow=state.Night?.7:0;return;}
        if(o.Kind=="egg")
        {
            bool nest=previous.Any(p=>p.Kind=="moss"&&Distance(o,p)<.14);
            if(nest)o.Growth++;
            o.Behavior=nest?"hatching":"egg_waiting";
            return;
        }
        if(o.Kind=="snail")
        {
            if(o.Age==600&&o.Generation>0)Discover("grown");
            bool moon=state.Night&&previous.Any(p=>p.Kind=="moonstone"&&Distance(o,p)<.16);
            bool wet=previous.Any(p=>p.Kind=="pond"&&Distance(o,p)<.13);
            if(o.Glow>=1&&(moon||wet))
            {
                o.Pollen++;if(o.Pollen>=100){o.Hue=moon?"moon":"blue";Discover("shell_"+o.Hue);o.Pollen=0;}
            }
            else o.Pollen=0;
            return;
        }
        if(o.Kind=="beetle")
        {
            var target=o.Dew==0
                ?previous.Where(p=>p.Kind=="pond").OrderBy(p=>Distance(o,p)).FirstOrDefault()
                :previous.Where(p=>p.Kind=="seed"&&p.Dew<100).OrderBy(p=>p.Growth>=100?1:0).ThenBy(p=>Distance(o,p)).FirstOrDefault();
            o.Behavior=target==null?"beetle_waiting":o.Dew==0?"collecting_dew":"carrying_dew";o.Following=target?.Id??-1;
            if(target!=null)
            {
                Walk(o,target.X,target.Y,.0022);
                if(Distance(o,target)<.05)
                {
                    o.Care++;
                    if(o.Care>=20)
                    {
                        o.Care=0;
                        if(o.Dew==0)o.Dew=1;
                        else
                        {state.Objects.Single(p=>p.Id==target.Id).Dew=180;o.Dew=0;Discover("watering");Trace("water_delivered",new{o.Id,Target=target.Id});}
                    }
                }
                else o.Care=0;
            }
        }
        if(o.Kind=="moth")
        {
            if(state.Night){o.Behavior="moth_resting";return;}
            var flowers=previous.Where(p=>p.Kind=="seed"&&p.Growth>=100).ToList();
            var target=flowers.Where(p=>p.Id!=o.Following||flowers.Count==1).OrderBy(p=>Distance(o,p)).FirstOrDefault();
            o.Behavior=target==null?"moth_waiting":"pollinating";
            if(target!=null)
            {
                Walk(o,target.X,target.Y,.003);
                if(Distance(o,target)<.04)
                {
                    o.Care++;
                    if(o.Care>=35)
                    {
                        o.Care=0;var flower=state.Objects.Single(p=>p.Id==target.Id);
                        if(o.Pollen>0)
                        {
                            flower.Pollen=Math.Min(120,flower.Pollen+60);
                            if(o.Hue!=flower.Hue&&flower.Hue!="pearl"){flower.Hue="pearl";Discover("flower_pearl");}
                            Discover("pollination");Trace("pollinated",new{o.Id,Target=flower.Id,flower.Hue});
                        }
                        o.Pollen=1;o.Hue=target.Hue;o.Following=target.Id;
                    }
                }
                else o.Care=0;
            }
        }
        if(o.Kind=="seed"&&o.Growth>=100&&o.Pollen>=60&&o.Cooldown==0)
        {
            Supply("seed",1);o.Pollen=0;o.Cooldown=900;Discover("seed_cycle");Trace("seed_produced",new{o.Id});
        }
        if(o.Kind=="seed"&&o.Dew>0)o.Dew--;
    }
    private void AdvanceEcosystem(GardenObject[] previous)
    {
        if(state.ExpeditionTicks>0&&--state.ExpeditionTicks==0)
        {
            var explorer=state.Objects.Single(o=>o.Id==state.Explorer);explorer.Away=false;explorer.X=.82;explorer.Y=.28;explorer.Behavior="returned";
            if(state.Trips==0){Supply("pond",1);Supply("beetle",1);Discover("expedition_pond");}
            else if(state.Trips==1){Supply("moth",1);Supply("moss",2);Discover("expedition_meadow");}
            else if(state.Trips==2){Supply("moonstone",1);Supply("seed",2);Discover("expedition_moon");}
            else {string[] finds=["grass","stone","snail","pond","moth","beetle","moss","moonstone"];Supply(finds[(state.Trips-3)%finds.Length],1);Supply("seed",1);Discover("return_trip");}
            state.Trips++;state.Explorer=-1;Trace("expedition_returned",new{state.Trips,Pouch=state.Pouch.ToDictionary(x=>x.Key,x=>x.Value)});
        }
        foreach(var egg in state.Objects.Where(o=>o.Kind=="egg"&&o.Growth>=180).ToArray())
        {egg.Kind="snail";egg.Age=0;egg.Growth=0;egg.Glow=egg.Glow>0?1:0;egg.Behavior="hatched";Discover("hatched");Trace("hatched",new{egg.Id,egg.Generation,egg.Hue});}
        if(state.Night&&state.Objects.Count<36)
        {
            foreach(var moss in previous.Where(o=>o.Kind=="moss"))
            {
                if(!previous.Any(o=>o.Kind=="pond"&&Distance(o,moss)<.2))continue;
                var parents=state.Objects.Where(o=>!o.Away&&o.Kind=="snail"&&o.Age>=600&&o.Cooldown==0&&Distance(o,moss)<.11).OrderBy(o=>o.Id).Take(2).ToArray();
                if(parents.Length<2)continue;
                parents[0].Care++;parents[1].Care++;
                if(parents.Min(o=>o.Care)<180)continue;
                foreach(var parent in parents){parent.Care=0;parent.Cooldown=2400;}
                if(state.Objects.Concat(state.Stored).Count(o=>o.Kind is "snail" or "egg")>=12)continue;
                var egg=new GardenObject{Id=NextId(),Kind="egg",X=moss.X,Y=moss.Y,Age=0,Generation=parents.Max(o=>o.Generation)+1,Hue=parents[0].Hue==parents[1].Hue?parents[0].Hue:"pearl",Glow=parents.Any(o=>o.Glow>=1)?1:0,Behavior="hatching"};
                state.Objects.Add(egg);Discover("egg");Trace("egg_laid",new{egg.Id,Parents=parents.Select(o=>o.Id).ToArray(),egg.Hue});
                if(state.Objects.Count>=36)break;
            }
        }
        bool harmony=state.Night&&state.Objects.Any(o=>o.Kind=="moonstone")&&state.Objects.Any(o=>o.Kind=="snail"&&o.Generation>0)&&state.Objects.Where(o=>o.Kind=="seed"&&o.Growth>=100).Select(o=>o.Hue).Distinct().Count()>=3;
        if(harmony)Discover("harmony");
    }
}
