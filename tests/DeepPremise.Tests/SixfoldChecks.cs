using DeepPremise.Core.Sixfold;
using DeepPremise.Core.Diagnostics;

static class SixfoldChecks
{
    public static void Run(Action<bool,string> check)
    {
        SixfoldRun Fixture(string kind,int clock,int poison=0,int shield=0)
        {
            var s=new SixfoldState{Phase="battle",Health=40,Shield=shield,Enemy=new Encounter{Health=1000,MaxHealth=1000,Armor=7,Interval=1000,Poison=poison},Body=new(){new Organ{Kind=kind,Clock=clock},null,null,null,null,null}};
            return SixfoldRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(s));
        }
        var piercing=Fixture("lance",27);piercing.Run(1);
        check(piercing.Observe().Enemy.Health==987,"Sixfold: lance bypasses armor");
        var catalyst=Fixture("catalyst",29,5);catalyst.Run(1);
        check(catalyst.Observe().Enemy.Poison==0&&catalyst.Observe().Enemy.Health==988,"Sixfold: catalyst consumes poison for immediate damage");
        var capacitor=Fixture("capacitor",31,0,10);capacitor.Run(1);
        check(capacitor.Observe().Shield==3&&capacitor.Observe().Enemy.Health==988,"Sixfold: capacitor trades protection for burst damage");
        var leech=Fixture("leechfang",18);leech.Run(1);
        check(leech.Observe().Health==43,"Sixfold: leech fang heals after its attack");
        var frost=Fixture("frost",24);frost.Run(1);
        check(frost.Observe().Enemy.Clock<0,"Sixfold: frost delays the next attack");
        var cannon=Fixture("cannon",39);cannon.Run(1);
        check(cannon.Observe().Enemy.Health==983,"Sixfold: cannon delivers a slow heavy hit");
        var a=new SixfoldRun(12);a.ChooseStarter(0);a.Take(0,2);a.Fight();a.Run(15);
        var mutationState=new SixfoldState{Phase="draft",Offers=new(){"venom"},Body=new(){new Organ{Kind="jaw",Level=3},null,null,null,null,null}};
        var mutation=SixfoldRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(mutationState));
        check(mutation.Take(0,0)&&mutation.Observe().Body[0] is {Kind:"venom",Level:3,Adapted:true},"Sixfold: replacing a capped organ preserves investment and grants temporary adaptation");
        var mutationCopy=SixfoldRun.LoadJson(mutation.SaveJson());
        check(mutationCopy.Observe().Body[0]!.Adapted,"Sixfold: adaptation survives saving before battle");
        var ended=mutation.Observe();ended.Phase="victory";mutation=SixfoldRun.LoadJson(System.Text.Json.JsonSerializer.Serialize(ended));mutation.Continue();
        check(!mutation.Observe().Body[0]!.Adapted,"Sixfold: adaptation expires at the next gate");
        var b=SixfoldRun.LoadJson(a.SaveJson());a.Run(200);b.Run(200);
        check(a.SaveJson()==b.SaveJson(),"Sixfold: exact deterministic combat continuation");
        var frozen=a.SaveJson();a.Run(100);check(frozen==a.SaveJson(),"Sixfold: reward screen never advances combat");
        check(!a.Take(0,0)&&!a.Upgrade(0),"Sixfold: phase guards prevent reward exploits");
        var detached=a.Observe();detached.Health=999;check(a.Observe().Health!=999,"Sixfold: detached observation cannot alter live state");
        check(SixfoldRun.Neighbors(1).Order().SequenceEqual(new[]{0,2,4}),"Sixfold: visible 3 by 2 layout matches adjacency");
        string directory;var logged=new SixfoldRun(6);
        using(var recorder=new PlaytestRecorder(Path.Combine(Path.GetTempPath(),"sixfold-tests"),logged,"test","en"))
        {directory=recorder.DirectoryPath;logged.ChooseStarter(0);recorder.Capture("starter");logged.Take(0,2);recorder.Capture("draft");logged.Fight();logged.Run(50);recorder.Capture("battle");}
        check(PlaytestReplay.Restore(directory).StateJson==logged.SaveJson(),"Sixfold: detailed recorded combat restores exactly");
        for(int starter=0;starter<3;starter++)
        {
            int first=0,wins=0;double floors=0;
            for(uint seed=1;seed<=60;seed++)
            {
                var run=new SixfoldRun(seed);run.ChooseStarter(starter);
                for(int step=0;step<65;step++)
                {
                    var s=run.Observe();if(s.Phase is "lost" or "won")break;
                    if(s.Phase=="draft")
                    {
                        string[] priority=starter==1?["venom","stomach","heart","shell","jaw","brood","lung","spike","eye"]:starter==2?["spike","shell","heart","stomach","jaw","venom","brood","lung","eye"]:["jaw","heart","stomach","shell","venom","brood","spike","lung","eye"];
                        int offer=Enumerable.Range(0,3).OrderBy(i=>Array.IndexOf(priority,s.Offers[i])).First();
                        int slot=s.Body.FindIndex(o=>o?.Kind==s.Offers[offer]&&o.Level<3);if(slot<0)slot=s.Body.FindIndex(o=>o==null);
                        if(slot<0)slot=Enumerable.Range(0,6).OrderByDescending(i=>Array.IndexOf(priority,s.Body[i]!.Kind)).First();
                        if(!run.Take(offer,slot)){for(int i=0;i<3&&run.Observe().Phase=="draft";i++)for(int j=0;j<6&&run.Observe().Phase=="draft";j++)run.Take(i,j);}
                    }
                    else if(s.Phase=="prepare")
                    {
                        while(run.Observe().Health<45&&run.Rest()){}
                        for(int i=0;i<6;i++)if(s.Body[i]?.Kind is "jaw" or "venom" or "spike")run.Upgrade(i);
                        run.Fight();run.Run(600);
                    }
                    else if(s.Phase=="victory"){if(s.Floor==1)first++;run.Continue();}
                    else if(s.Phase=="relic")run.ChooseRelic(0);
                }
                var end=run.Observe();floors+=end.Floor;if(end.Phase=="won")wins++;
                check(end.Phase is "lost" or "won",$"Sixfold: seed {seed}, starter {starter} reaches a bounded ending");
            }
            Console.WriteLine($"SIXFOLD BALANCE starter={starter} first={first}/60 wins={wins}/60 meanGate={floors/60:0.0}");
        }
    }
}
