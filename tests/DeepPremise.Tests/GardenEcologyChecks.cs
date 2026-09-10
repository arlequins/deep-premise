using System.Text.Json.Nodes;
using DeepPremise.Core.Garden;
using DeepPremise.Core.Diagnostics;

static class GardenEcologyChecks
{
    public static void Run(Action<bool,string> check)
    {
        var world=new GardenWorld();
        check(!world.StartExpedition(),"Garden ecology: exploration requires a developed garden");
        world.ToggleLight();world.Move(1,.47,.54);world.Run(100);world.Move(1,.87,.25);world.Run(1);
        world.Move(1,.1,.1);world.Move(2,.7,.6);world.Move(0,.74,.6);world.Place("seed",.72,.61);world.Run(100);
        check(world.StartExpedition(),"Garden ecology: the first bloom opens an expedition");
        check(!world.StartExpedition()&&!world.Move(1,.5,.5)&&!world.Store(1),"Garden ecology: an explorer cannot be duplicated, moved, or packed mid-trip");
        world.Run(80);var resumed=GardenWorld.LoadJson(world.SaveJson());world.Run(160);resumed.Run(160);
        check(world.SaveJson()==resumed.SaveJson()&&world.Observe().Trips==1&&world.Observe().Pouch["pond"]==1,"Garden ecology: expedition saves resume to exactly one return and reward");
        check(world.ExpeditionRequirement()=="need_water","Garden ecology: later paths require ecological discoveries, not repeated waiting");
        world.Place("pond",.5,.5);world.Place("beetle",.51,.5);world.Place("seed",.61,.5);world.Place("grass",.62,.51);world.Run(450);
        check(world.Observe().Discoveries.Contains("watering")&&world.Observe().Discoveries.Contains("flower_blue"),"Garden ecology: beetles deliver water and wet shelter grows blue flowers");
        check(world.StartExpedition(),"Garden ecology: water delivery opens the meadow");world.Run(360);
        check(world.Observe().Pouch["moth"]==1&&world.Observe().Pouch["moss"]==2,"Garden ecology: meadow exploration brings pollinators and nesting habitat");
        world.Place("moth",.7,.61);world.ToggleLight();world.Run(1100);
        check(world.Observe().Discoveries.Contains("pollination")&&world.Observe().Discoveries.Contains("seed_cycle")&&world.Observe().Pouch["seed"]>0,"Garden ecology: moth pollination renews the seed supply");
        check(world.Observe().Discoveries.Contains("flower_pearl"),"Garden ecology: pollen from another color creates a pearly flower");
        check(world.StartExpedition(),"Garden ecology: color and pollination discoveries open the moonwoods");world.Run(480);
        check(world.Observe().Pouch["moonstone"]==1,"Garden ecology: the moonwoods grant a new environmental influence");
        world.Place("moonstone",.25,.7);world.Place("grass",.27,.7);world.Place("seed",.26,.69);world.ToggleLight();world.Run(100);
        check(world.Observe().Discoveries.Contains("flower_moon"),"Garden ecology: moonlight grows a distinct flower rather than a numeric upgrade");
        int second=world.Place("snail",.74,.61);world.Run(80);world.Place("moss",.46,.5);world.Move(1,.46,.5);world.Move(second,.47,.5);world.Run(400);
        var child=world.Observe().Objects.FirstOrDefault(o=>o.Kind=="snail"&&o.Generation==1);
        check(child!=null&&child.Glow>=1&&child.Age<600,"Garden ecology: two adults in a wet nest produce a young snail with inherited light");
        if(child!=null)
        {
            var savedChild=world.Observe().Objects.Single(o=>o.Id==child.Id);
            check(world.Store(child.Id),"Garden ecology: an individual can be safely packed away");
            int placed=world.Place("snail",.4,.4);
            var unpacked=world.Observe().Objects.Single(o=>o.Id==placed);
            check(placed==child.Id&&unpacked.Generation==savedChild.Generation&&unpacked.Hue==savedChild.Hue&&unpacked.Age==savedChild.Age,"Garden ecology: unpacking retains identity, ancestry, color, and age");
        }
        var old=JsonNode.Parse(new GardenWorld().SaveJson())!;old["Version"]=10;old.AsObject().Remove("Stored");old.AsObject().Remove("Discoveries");old["Pouch"]!.AsObject().Remove("pond");
        var migrated=GardenWorld.LoadJson(old.ToJsonString()).Observe();
        check(migrated.Version==11&&migrated.Objects.Count==3&&migrated.Pouch["pond"]==0,"Garden ecology: old saves migrate without resetting the garden");
        world.Run(15000);var longState=world.Observe();
        check(longState.Objects.Count<=36&&longState.Pouch.Values.All(n=>n>=0&&n<=24)&&longState.Objects.All(o=>double.IsFinite(o.X)&&double.IsFinite(o.Y)),"Garden ecology: extended unattended growth remains bounded and valid");
        string directory;using(var recorder=new PlaytestRecorder(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"deep-premise-ecology-tests"),world,"type3-0.2-test","en"))
        {directory=recorder.DirectoryPath;world.Run(50);world.Store(0);recorder.Capture("store");}
        check(PlaytestReplay.Restore(directory).StateJson==world.SaveJson(),"Garden ecology: expanded ecology and storage restore exactly from local logs");
    }
}
