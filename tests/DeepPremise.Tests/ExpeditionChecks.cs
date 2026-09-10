using System.Text.Json;
using DeepPremise.Core.Defense;
using DeepPremise.Core.Diagnostics;

static class ExpeditionChecks
{
    public static void Run(Action<bool,string> check)
    {
        static SalvageRun World(RunState state) => SalvageRun.LoadJson(JsonSerializer.Serialize(state));
        static RunState Quiet() => new() { Phase="fight", Pending=1, SpawnTimer=1000 };
        static Intruder Enemy(int id,int lane,double x) => new() { Id=id,Lane=lane,X=x,Health=100,MaxHealth=100 };

        var collision=Quiet();
        var ball=Enemy(1,0,3);ball.Oil=70;ball.Burn=40;
        collision.Enemies=[ball,Enemy(2,1,3.6),Enemy(3,2,3.6)];
        var aimed=World(collision);var missed=World(collision);
        check(aimed.Sling(1,1)&&missed.Sling(1,0),"Expedition: player can choose different sling destinations");
        aimed.Run(8);missed.Run(8);
        check(aimed.Observe().Enemies.Single(e=>e.Id==2).Health==100,
            "Expedition: airborne travel cannot hit the destination before landing");
        aimed.Run(1);missed.Run(1);
        var struck=aimed.Observe().Enemies.Single(e=>e.Id==2);
        check(struck.Health==95&&struck.Oil>0&&struck.Burn>0&&struck.X>3.6&&
            missed.Observe().Enemies.Single(e=>e.Id==2).Health==100&&
            aimed.Observe().Enemies.Single(e=>e.Id==3).Health==100,
            "Expedition: aiming into a crowd transfers burning resin and damage only to the destination lane");
        aimed.Run(15);
        check(aimed.Observe().Combo==1&&aimed.Observe().Enemies.Single(e=>e.Id==2).Health>90,
            "Expedition: one flight hits each victim once, while transferred fire keeps burning");
        check(!aimed.Sling(2,0),"Expedition: sling cannot be spammed during its shared cooldown");

        var catcher=Quiet();catcher.Rigs=[new(){Cell=10,Kind="collector",Cooldown=100}];
        catcher.Enemies=[Enemy(1,0,3.2)];
        var caught=World(catcher);var bypassed=World(catcher);
        caught.Sling(1,1);bypassed.Sling(1,2);caught.Run(9);bypassed.Run(9);
        check(caught.Observe().Enemies.Count==0&&caught.Observe().Kills==1&&
            caught.Observe().Scrap==bypassed.Observe().Scrap+6&&bypassed.Observe().Enemies.Count==1&&caught.Observe().Rigs[0].Captured=="runner",
            "Expedition: landing a live enemy in a collector converts it into extra salvage");

        var graft=Quiet();graft.Rigs=[new(){Cell=10,Kind="collector",Captured="armored",Shield=1,Cooldown=100}];
        graft.Enemies=[Enemy(1,1,-.34),Enemy(2,1,-.34),Enemy(3,2,-.34)];
        var shielded=World(graft);shielded.Run(1);
        check(shielded.Observe().Hull==16&&shielded.Observe().Rigs[0].Shield==0,
            "Expedition: armor graft absorbs one breach in its lane, not every breach or adjacent lanes");
        graft=Quiet();graft.Rigs=[new(){Cell=10,Kind="collector",Captured="sprinter",Cooldown=100},new(){Cell=11,Kind="spark"}];
        graft.Enemies=[Enemy(1,1,4.5)];var haste=World(graft);
        graft.Rigs[0].Captured="none";var normal=World(graft);
        haste.Run(10);normal.Run(10);
        check(haste.Observe().Enemies[0].Health<normal.Observe().Enemies[0].Health,
            "Expedition: sprinter graft lets nearby equipment land an extra attack in the same time");
        var stalled=Quiet();stalled.Pending=0;stalled.Rigs=[new(){Cell=10,Kind="collector"}];stalled.Enemies=[Enemy(1,2,7)];
        var farming=World(stalled);farming.Run(100);
        check(farming.Observe().Scrap==16,"Expedition: stalling the last enemy cannot farm passive collector income");

        var heldState=Quiet();heldState.Enemies=[Enemy(1,0,3)];heldState.Rigs=[new(){Cell=3,Kind="spark"}];
        var held=World(heldState);check(held.Grab(1),"Expedition: an enemy can be grabbed for a deliberate throw");held.Run(30);
        check(held.Observe().Enemies[0].X==3&&held.Observe().Enemies[0].Health==100,
            "Expedition: grabbing preserves the projectile while the player aims");
        held.Run(31);
        check(held.Observe().HeldEnemy==-1&&held.Observe().Enemies[0].X<3,
            "Expedition: a held enemy resumes moving when the aiming window expires");

        var route=Quiet();route.Mission="cache";
        var early=World(route);early.Dispatch();early.Run(59);
        check(early.Observe().CargoTier==1,"Expedition: halfway travel earns bankable partial cargo");
        var deep=SalvageRun.LoadJson(early.SaveJson());
        early.RecallCourier();early.Run(200);deep.Run(200);
        check(early.Observe().MissionComplete&&deep.Observe().MissionComplete&&
            early.Observe().Scrap==19&&deep.Observe().Scrap==28,
            "Expedition: recalling banks five scrap while deeper extraction banks fourteen");

        var peril=route;peril.Tick=59;peril.WaveTick=59;peril.Dispatched=true;
        peril.CourierPhase="outbound";peril.CourierX=3.245;peril.CourierHealth=1;peril.Cargo=true;peril.CargoTier=1;
        peril.Enemies=[Enemy(1,0,5)];
        var retreat=World(peril);var greed=World(peril);
        retreat.RecallCourier();retreat.Run(100);greed.Run(100);
        check(retreat.Observe().MissionComplete&&retreat.Observe().CourierPhase=="home"&&
            greed.Observe().CourierPhase=="lost"&&!greed.Observe().MissionComplete,
            "Expedition: recalling before an approaching threat saves cargo that deeper travel loses");

        var deadline=Quiet();deadline.WaveTick=100;
        var inTime=World(deadline);check(inTime.Dispatch(),"Expedition: dispatch remains possible at the deadline");
        deadline.WaveTick=101;deadline.Pending=0;deadline.Enemies=[Enemy(1,2,7)];
        var late=World(deadline);var before=late.SaveJson();
        check(!late.Dispatch()&&before==late.SaveJson(),"Expedition: waiting for the last enemy cannot buy a late safe extraction");

        var debtState=new RunState{Phase="fight",Tick=70,Relic="debt"};
        var debt=World(debtState);debt.Pulse(0);debt.Run(4);
        check(debt.Observe().Phase=="fight"&&debt.Observe().Debt==2,
            "Expedition: an empty battlefield cannot clear while a borrowed pulse is unpaid");
        debt.Run(1);
        check(debt.Observe().Debt==0&&debt.Observe().Pending+debt.Observe().Enemies.Count==2&&debt.Observe().Phase=="fight",
            "Expedition: borrowed power becomes two actual enemies before the wave can clear");

        var persistence=Quiet();persistence.Crew=["scout"];persistence.Relics=["relay"];
        persistence.Relic="relay";persistence.Enemies=[ball,Enemy(2,1,3.6)];
        var live=World(persistence);live.Dispatch();live.Sling(1,1);live.Run(11);
        var restored=SalvageRun.LoadJson(live.SaveJson());
        check(restored.Observe().Enemies.Single(e=>e.Id==1).Collided.Contains(2)&&
            restored.Observe().SlingCooldown>0&&restored.Observe().WaveTick==11,
            "Expedition: save retains collision history, flight cooldown and mission clock");
        live.Run(50);restored.Run(50);
        check(live.SaveJson()==restored.SaveJson(),"Expedition: saving during flight and a courier journey preserves exact continuation");
        var recordRoot=Path.Combine(Path.GetTempPath(),"expedition-checks-"+Guid.NewGuid().ToString("N"));
        string directory;
        using(var recorder=new PlaytestRecorder(recordRoot,live,"test","en"))
        {
            directory=recorder.DirectoryPath;live.Run(10);live.RecallCourier();live.Run(70);
        }
        check(PlaytestReplay.Restore(directory).StateJson==live.SaveJson(),
            "Expedition: replay reconstructs partial cargo recall, crew, relic and sling state");
    }
}
