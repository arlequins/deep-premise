using DeepPremise.Core.Habits;
using DeepPremise.Core.Diagnostics;

static class HabitChecks
{
    public static void Run(Action<bool,string> check)
    {
        var a=new HabitWorld(17);var b=new HabitWorld(17);a.Run(470);b.Run(470);
        check(a.SaveJson()==b.SaveJson(),"Habits: autonomous labor is deterministic");
        check(a.Observe().Sites[0].Bound&&a.Observe().Sites[3].Claimed,"Habits: repeated cooperation and craft change local material rules");
        var resumed=HabitWorld.LoadJson(a.SaveJson());a.Run(50);resumed.Run(50);
        check(a.SaveJson()==resumed.SaveJson(),"Habits: a changed world survives exact save continuation");
        var separate=new HabitWorld(17);separate.ToggleTogether(0);separate.Run(470);
        check(!separate.Observe().Sites[0].Bound,"Habits: separate carrying prevents a pair expectation from forming");
        var moved=new HabitWorld(17);moved.Run(470);moved.Assign(0,1);moved.Run(100);
        check(moved.Observe().Sites[0].EchoSeen&&moved.Observe().Sites[0].EchoTrips>0,"Habits: a familiar path repeats after its worker changes tasks");
        check(moved.Observe().People.First(p=>p.Id==1).Thought=="too_heavy","Habits: removing one partner visibly stops previously shared carrying");
        moved.TogglePractice(0);moved.Run(650);
        check(!moved.Observe().Sites[0].Bound,"Habits: repeated empty-handed practice releases the pair constraint");
        moved.TogglePractice(0);moved.Run(120);
        check(moved.Observe().People.First(p=>p.Id==1).Thought!="too_heavy","Habits: a lone worker can produce again after changing the habit");
        var loom=new HabitWorld(17);loom.Run(470);loom.Assign(4,2);loom.Assign(2,3);loom.Run(140);
        check(loom.Observe().People.First(p=>p.Id==2).Thought=="wrong_hands","Habits: an unfamiliar worker encounters a loom's learned preference");
        var clothBefore=loom.Observe().Stock["cloth"];loom.Assign(4,3);loom.Run(160);
        check(loom.Observe().People.Any(p=>p.Job==3&&p.Cargo>0)||loom.Observe().Stock["cloth"]>clothBefore,"Habits: returning the familiar maker permits new production");
        var guests=new HabitWorld(17);guests.Run(450);check(guests.Observe().GuestWaiting&&guests.Admit()&&guests.Observe().People.Count==6&&!guests.Admit(),"Habits: material production admits a new resident exactly once");
        var local=Path.Combine(Path.GetTempPath(),"habit-test-"+Guid.NewGuid().ToString("N"));string session;
        var recorded=new HabitWorld(17);using(var recorder=new PlaytestRecorder(local,recorded,"test","ko")){session=recorder.DirectoryPath;recorded.Run(500);recorded.Assign(0,1);recorder.Capture("assignment");recorded.Run(80);}
        check(PlaytestReplay.Restore(session).StateJson==recorded.SaveJson(),"Habits: production, learned laws and echoes reconstruct from detailed logs");
        var settlement=new HabitWorld(17);
        for(int tick=0;tick<2100;tick++)
        {
            var view=settlement.Observe();if(view.GuestWaiting&&view.People.Count<7){settlement.Admit();if(view.People.Count==5)settlement.Assign(5,2);}
            settlement.Run(1);
        }
        check(settlement.Observe().Established&&!settlement.Observe().Failed&&settlement.Observe().People.Count==7,"Habits: balanced reassignment supports a seven-person settlement through the objective");
    }
}
