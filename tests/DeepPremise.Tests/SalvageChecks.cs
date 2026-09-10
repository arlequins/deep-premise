using DeepPremise.Core.Defense;
using DeepPremise.Core.Diagnostics;

static class SalvageChecks
{
    public static void Run(Action<bool,string> check)
    {
        SalvageRun Opening(uint seed) { var r=new SalvageRun(seed);foreach(var cell in new[]{4,11,18})r.Place(cell,"spark");r.Launch();return r; }
        var a=Opening(42);var b=Opening(42);a.Run(120);b.Run(120);
        check(a.SaveJson()==b.SaveJson(),"Salvage: seeded combat is deterministic");
        var restored=SalvageRun.LoadJson(a.SaveJson());a.Run(90);restored.Run(90);
        check(a.SaveJson()==restored.SaveJson(),"Salvage: mid-fight save continues exactly");
        check(!a.Place(0,"spark")&&!a.Sell(4),"Salvage: battle prevents equipment edits");
        var build=new SalvageRun(1);check(!build.Launch(),"Salvage: cannot launch an empty defense");
        build.Place(4,"flame");build.Place(4,"flame");build.Sell(4);
        check(build.Observe().Scrap==16&&build.Observe().Rigs.Count==0,"Salvage: repacking refunds original and upgrade costs");
        var pulse=Opening(2);pulse.Run(40);check(pulse.Pulse(0)&&!pulse.Pulse(1),"Salvage: pulse shares one cooldown across lanes");
        var sample=Opening(42);sample.Run(1800);
        check(sample.Observe().Phase=="reward" && sample.Observe().Hull>=12,"Salvage: suggested opening survives first wave");
        var choices=sample.Observe().Rewards.ToArray();check(choices.Length==3&&choices.Distinct().Count()==3,"Salvage: three distinct rewards offered");
        check(!sample.Choose("invalid")&&sample.Choose(choices[0])&&!sample.Choose(choices[0]),"Salvage: one valid reward advances the run once");
        var comboState=new RunState{Phase="fight",Pending=0,Rigs=new(){new(){Cell=4,Kind="oil"},new(){Cell=5,Kind="flame"}},Enemies=new(){new(){Id=1,Lane=0,X=4.6,Health=30,MaxHealth=30},new(){Id=2,Lane=0,X=4.7,Health=30,MaxHealth=30}}};
        var combo=SalvageRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(comboState));combo.Run(1);
        check(combo.Observe().Combo==1&&combo.Observe().Enemies.All(e=>e.Health<30),"Salvage: resin and flame create an area explosion");
        comboState.Rigs=new(){new(){Cell=4,Kind="shifter"}};
        var shift=SalvageRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(comboState));shift.Run(1);
        check(shift.Observe().Enemies.Any(e=>e.Lane==1),"Salvage: deflector physically changes an enemy lane");
        comboState.Rigs=new(){new(){Cell=4,Kind="spark"}};comboState.Weather="rain";
        var rainy=SalvageRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(comboState));rainy.Run(1);
        check(rainy.Observe().Enemies.All(e=>e.Health<30),"Salvage: rain conducts an arc to a second enemy");
        var weak=new SalvageRun(4);weak.Place(4,"spark");weak.Launch();weak.Run(2500);
        check(weak.Observe().Hull<sample.Observe().Hull,"Salvage: leaving lanes uncovered costs hull");
        var sessionRoot=Path.Combine(Path.GetTempPath(),"salvage-tests-"+Guid.NewGuid().ToString("N"));string directory;
        var recorded=Opening(13);using(var recorder=new PlaytestRecorder(sessionRoot,recorded,"test","ko")){directory=recorder.DirectoryPath;recorded.Run(130);}
        check(PlaytestReplay.Restore(directory).StateJson==recorded.SaveJson(),"Salvage: full combat state reconstructs from logs");
        var victories=0;var losses=0;
        for(uint seed=1;seed<=12;seed++)
        {
            var r=new SalvageRun(seed);
            for(int wave=0;wave<8;wave++)
            {
                foreach(var cell in new[]{4,11,18,3,10,17,5,12,19,2,9,16,6,13,20})r.Place(cell,cell%7==5?"oil":cell%7==3?"flame":"spark");
                foreach(var rig in r.Observe().Rigs)r.Place(rig.Cell,rig.Kind);
                r.Launch();
                for(int t=0;t<2500&&r.Observe().Phase=="fight";t++){if(t%110==0){var s=r.Observe();var lane=s.Enemies.OrderBy(e=>e.X).FirstOrDefault()?.Lane??0;r.Pulse(lane);}r.Run(1);}
                var state=r.Observe();if(state.Phase=="lost"){losses++;break;}if(state.Phase=="won"){victories++;break;}
                if(state.Phase!="reward")break;
                var reward=state.Hull<12&&state.Rewards.Contains("repair")?"repair":state.Rewards.Contains("power")?"power":state.Rewards[0];r.Choose(reward);
            }
        }
        Console.WriteLine($"SALVAGE BALANCE: {victories}/12 wins, {losses}/12 losses with a reinvesting scripted strategy");
        check(victories+losses==12&&victories>0,"Salvage: complete seeded runs terminate and can be won");
    }
}
