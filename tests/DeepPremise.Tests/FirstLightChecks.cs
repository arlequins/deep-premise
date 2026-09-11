using System.Text.Json;
using DeepPremise.Core.FirstLight;
using DeepPremise.Core.Diagnostics;

static class FirstLightChecks
{
    public static void Run(Action<bool,string> check)
    {
        var world=new LightWorld();world.Run(200);
        check(world.Observe().Entities[0].Kind=="egg"&&world.Observe().Entities[0].Age==0,"First light: continuous observation freezes the seed exactly");
        check(world.Observe().Entities.Any(e=>e.Kind=="plant"&&e.Age>=180),"First light: the unseen world grows while the seed is frozen");
        world.Aim(.05,.05);world.Run(80);var life=world.Observe().Entities.First(e=>e.Kind=="life");
        world.Aim(life.X,life.Y);string before=JsonSerializer.Serialize(world.Observe().Entities.First(e=>e.Id==life.Id));world.Run(50);
        check(JsonSerializer.Serialize(world.Observe().Entities.First(e=>e.Id==life.Id))==before,"First light: light freezes movement, hunger, growth, and reproduction together");
        var memory=world.Observe().Memories.First(m=>m.Entity.Id==life.Id);world.Aim(.05,.05);world.Run(40);
        check(world.Observe().Memories.First(m=>m.Entity.Id==life.Id).SeenAt==memory.SeenAt,"First light: unseen silhouettes retain the last observation rather than reveal current movement");
        var restored=LightWorld.LoadJson(world.SaveJson());world.Run(70);restored.Run(70);
        check(world.SaveJson()==restored.SaveJson(),"First light: deterministic continuation retains light, shadows, hunger, and memory");
        var scene=new LightState{LightOn=true,LightX=.5,LightY=.5,Entities=new(){new(){Id=0,Kind="plant",X=.5,Y=.5,Age=200,Fruit=2},new(){Id=1,Kind="life",X=.62,Y=.5,Meals=1}}};
        var protectedFood=LightWorld.LoadJson(JsonSerializer.Serialize(scene));protectedFood.Run(70);
        check(protectedFood.Observe().Entities[0].Fruit==2&&protectedFood.Observe().Entities[1].Meals==1,"First light: dark consumers cannot consume a frozen resource across the light boundary");
        var unattended=new LightWorld();unattended.Aim(.05,.05,false);unattended.Run(3000);
        check(unattended.Observe().Lost,"First light: leaving everything dark has a real consequence, not an automatic win");
        var guided=new LightWorld();guided.Aim(.05,.05,false);
        for(int t=0;t<2200&&!guided.Observe().Lost&&!guided.Observe().Won;t++)
        {
            var s=guided.Observe();var hunter=s.Entities.FirstOrDefault(e=>e.Kind=="hunter");int adults=s.Entities.Count(e=>e.Alive&&e.Kind=="life"&&e.Meals>=3);
            if(hunter!=null&&!(adults>=3&&s.Entities.Where(e=>e.Kind=="plant").All(e=>e.Age>=850)))guided.Aim(hunter.X,hunter.Y);
            else guided.Aim(.05,.05,false);
            guided.Run(1);
        }
        check(guided.Observe().Won,"First light: holding the hunter while forests and lives develop permits a complete dark release victory");
        if(!guided.Observe().Won)Console.WriteLine("LIGHT DIAGNOSTIC "+guided.SaveJson());
        var success=guided.Observe();check(success.IndependentTicks>=240&&!success.LightOn,"First light: victory requires sustained survival without protective light");
        string directory;var logged=new LightWorld();using(var recorder=new PlaytestRecorder(Path.Combine(Path.GetTempPath(),"deep-premise-light-tests"),logged,"type10-test","en"))
        {directory=recorder.DirectoryPath;logged.Aim(.05,.05);logged.Run(100);}
        check(PlaytestReplay.Restore(directory).StateJson==logged.SaveJson(),"First light: local records restore exact state and last-seen information");
    }
}
