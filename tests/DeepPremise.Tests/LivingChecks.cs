using System.Text.Json;
using DeepPremise.Core.Defense;

static class LivingChecks
{
    public static void Run(Action<bool,string> check)
    {
        static SalvageRun Load(RunState s)=>SalvageRun.LoadJson(JsonSerializer.Serialize(s));
        var hungry=new RunState{Phase="fight",Pending=1,SpawnTimer=1000,Rigs=[new(){Cell=11,Kind="collector",Captured="runner",Satiation=1,Growth=2}]};
        var starving=Load(hungry);starving.Run(1);var escaped=starving.Observe();
        check(escaped.Rigs[0].Captured=="none"&&escaped.Enemies.Single().Escaped&&escaped.Enemies[0].Lane==1&&escaped.Enemies[0].X<4.5&&escaped.Enemies[0].MaxHealth==26,"Living: a starved machine releases its grown captive inside its own lane");
        hungry.Rigs[0].Satiation=60;hungry.Enemies=[new(){Id=1,Lane=0,X=4.2,Health=100,MaxHealth=100}];hungry.NextId=1;
        var fed=Load(hungry);fed.Sling(1,1,4.2);fed.Run(9);
        check(fed.Observe().Rigs[0].Satiation==360&&fed.Observe().Rigs[0].Growth==3,"Living: feeding the same species restores hunger and grows the machine");
        hungry.Enemies[0].Kind="sprinter";var mutated=Load(hungry);mutated.Sling(1,1,4.2);mutated.Run(9);
        check(mutated.Observe().Rigs[0].Captured=="sprinter"&&mutated.Observe().Rigs[0].Growth==1,"Living: different prey changes the machine species instead of stacking every power");
        var layout=fed.Observe();layout.Phase="build";var moving=Load(layout);var funds=layout.Scrap;
        check(moving.MoveRig(11,12)&&moving.Observe().Rigs[0].Growth==3&&moving.Observe().Scrap==funds&&!moving.MoveRig(12,12),"Living: rearranging preserves the fed creature and rejects occupied destinations");
        var held=new RunState{Phase="fight",Pending=1,SpawnTimer=1000,Enemies=[new(){Id=1,Lane=0,X=5,Health=100,MaxHealth=100}]};
        var cancel=Load(held);cancel.Grab(1);cancel.CancelGrab();check(!cancel.Grab(1)&&cancel.Observe().SlingCooldown>0,"Living: cancelling a grab cannot indefinitely freeze an enemy for free");
        var opening=SalvageRun.Adventure(7);opening.Launch();
        check(opening.Observe().Rigs.Any(r=>r.Kind=="collector")&&opening.Observe().Enemies.Any(e=>e.Oil>0)&&opening.Observe().Enemies.Count(e=>e.Lane==1)>=3,"Living: opening offers a ready capture machine and an actual bowling opportunity");
        var armored=new RunState{Phase="fight",Pending=1,SpawnTimer=1000,Rigs=[new(){Cell=4,Kind="spark"}],Enemies=[new(){Id=1,Kind="armored",Lane=0,X=4.5,Health=100,MaxHealth=100}]};
        var intact=Load(armored);armored.Enemies[0].Cracked=true;var cracked=Load(armored);intact.Run(1);cracked.Run(1);
        check(cracked.Observe().Enemies[0].Health<intact.Observe().Enemies[0].Health,"Living: breaking armor makes subsequent machine attacks materially stronger");
        var crisis=new RunState{Phase="fight",Hull=1,Pending=0,Rigs=[new(){Cell=0,Kind="collector"}],Enemies=[new(){Id=1,Lane=0,X=.1,Health=100,MaxHealth=100},new(){Id=2,Lane=1,X=4,Kind="armored",Health=100,MaxHealth=100}]};
        var idle=Load(crisis);var improvised=Load(crisis);improvised.Sling(2,0,.5);idle.Run(60);improvised.Run(60);
        check(idle.Observe().Phase=="lost"&&improvised.Observe().Phase=="reward"&&improvised.Observe().Hull==1,
            "Living: an otherwise fatal breach is survived by throwing an armored enemy into a magnet to create a shield in time");
    }
}
