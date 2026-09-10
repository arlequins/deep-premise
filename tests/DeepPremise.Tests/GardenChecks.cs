using DeepPremise.Core.Garden;
using DeepPremise.Core.Diagnostics;

static class GardenChecks
{
    public static void Run(Action<bool,string> check)
    {
        var a=new GardenWorld();var b=new GardenWorld();
        a.ToggleLight();b.ToggleLight();a.Move(1,.47,.54);b.Move(1,.47,.54);a.Run(100);b.Run(100);
        check(a.SaveJson()==b.SaveJson(),"Garden: identical interventions produce identical ecology");
        check(a.Observe().DiscoveredGlow && a.Observe().Objects.Single(o=>o.Id==1).Glow==1,"Garden: sustained nearby warmth changes a snail's shell");
        var day=new GardenWorld();day.Move(1,.46,.5);day.Run(65);
        check(!day.Observe().DiscoveredGlow,"Garden: a cold daytime stone does not grant a glowing shell");
        a.Move(2,.52,.55);a.Run(1);
        check(a.Observe().DiscoveredFollowing && a.Observe().Objects.Single(o=>o.Id==2).Following==1,"Garden: grass identifies a nearby glowing creature as its target");
        a.Move(2,.05,.9);a.Run(1);
        check(a.Observe().Objects.Single(o=>o.Id==2).Following==-1,"Garden: distant grass cannot sense light across the whole garden");
        a.Move(1,.87,.25);a.Run(1);
        check(a.Observe().DiscoveredSeed && a.Observe().Pouch["seed"]==2,"Garden: an illuminated hollow reveals a finite seed supply");
        a.Run(10);check(a.Observe().Pouch["seed"]==2,"Garden: revisiting the hollow cannot duplicate its reward");
        a.Move(1,.1,.1);a.Move(2,.7,.6);a.Move(0,.74,.6);int seed=a.Place("seed",.72,.61);a.Run(100);
        check(a.Observe().DiscoveredBloom && a.Observe().Objects.Single(o=>o.Id==seed).Growth==100,"Garden: shelter and nighttime warmth together grow a luminous flower");
        a.Run(1);check(a.Observe().Objects.Single(o=>o.Id==2).Following==seed,"Garden: the new flower competes for the grass's attention");
        var loaded=GardenWorld.LoadJson(a.SaveJson());a.Run(340);loaded.Run(340);
        check(a.SaveJson()==loaded.SaveJson(),"Garden: save continuation preserves learned traits and motion");
        var before=a.SaveJson();check(a.Place("unknown",.5,.5)==-1 && !a.Move(1,double.NaN,.2)&&a.SaveJson()==before,"Garden: invalid interventions cannot corrupt state or consume supplies");
        var view=a.Observe();view.Objects[0].X=.9;check(a.SaveJson()==before,"Garden: observed state is detached");
        string directory;var traceWorld=new GardenWorld();
        using(var recorder=new PlaytestRecorder(Path.Combine(Path.GetTempPath(),"deep-premise-garden-tests"),traceWorld,"type3-test","en"))
        {directory=recorder.DirectoryPath;traceWorld.ToggleLight();traceWorld.Move(1,.47,.54);traceWorld.Run(100);}
        check(PlaytestReplay.Restore(directory).StateJson==traceWorld.SaveJson(),"Garden: local decision log restores the exact living garden");
    }
}
