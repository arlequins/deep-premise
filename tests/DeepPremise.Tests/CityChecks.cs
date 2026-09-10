using DeepPremise.Core.City;
using DeepPremise.Core.Diagnostics;

static class CityChecks
{
    public static void Run(Action<bool,string> check)
    {
        var a = new CityWorld(31); var b = new CityWorld(31);
        a.Run(960); b.Run(960);
        check(a.SaveJson()==b.SaveJson(),"City: identical seeds preserve deterministic daily life");
        var loaded=CityWorld.LoadJson(a.SaveJson()); a.Run(317); loaded.Run(317);
        check(a.SaveJson()==loaded.SaveJson(),"City: save continuation preserves routes, preferences, growth and treasury");
        var state=a.Observe();
        check(state.People.Count>9 && state.Tiles.Any(t=>t.Kind=="home"&&t.Level==2),"City: stable households attract neighbors and expand");
        check(state.Tiles.Any(t=>t.Kind=="park"&&t.Level==2),"City: autonomous repeated visits establish a local gathering place");
        var detached=a.Observe();detached.Tiles[0].Kind="river";
        check(a.Observe().Tiles[0].Kind!="river","City: observation cannot mutate the simulation");
        var isolated=new CityWorld();isolated.Build(6*CityWorld.Width+3,"erase");
        check(isolated.Observe().Tiles.Where(t=>t.Kind=="home").All(t=>!t.Connected),"City: cutting the entrance route disconnects buildings beyond it");
        isolated.Run(60);
        check(isolated.Observe().People.All(p=>p.At==p.Home && p.Work==-1),"City: disconnected workplaces do not create jobs or teleport commuters");
        isolated.Build(6*CityWorld.Width+3,"road");
        check(isolated.Observe().Tiles.Where(t=>t.Kind=="home").All(t=>t.Connected),"City: rebuilding the street restores connectivity");
        var building=new CityWorld();var before=building.SaveJson();
        check(building.Build(5*CityWorld.Width+4,"erase")=="occupied" && building.SaveJson()==before,"City: removing occupied homes is rejected without mutations");
        check(building.Build(16,"home")=="protected","City: river cannot be built over");
        int funds=building.Observe().Funds;building.Build(4*CityWorld.Width+3,"park");
        check(building.Observe().Funds==funds-20,"City: construction deducts its exact price");
        var traceWorld=new CityWorld();string directory;
        using(var recorder=new PlaytestRecorder(Path.Combine(Path.GetTempPath(),"deep-premise-city-tests"),traceWorld,"type2-test","en"))
        { directory=recorder.DirectoryPath;traceWorld.Run(245);traceWorld.Build(4*CityWorld.Width+3,"road");recorder.Capture("build"); }
        check(PlaytestReplay.Restore(directory).StateJson==traceWorld.SaveJson(),"City: detailed local trace restores exact state after construction");
    }
}
