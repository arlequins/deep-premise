using DeepPremise.Core.Defense;
using DeepPremise.Core.Diagnostics;

static class DefenseChecks
{
    public static void Run(Action<bool,string> check)
    {
        var a=new DefenseSimulation(3); var b=new DefenseSimulation(3);
        a.Run(60); b.Run(60); check(a.SaveJson()==b.SaveJson(),"Defense: identical seeds reproduce observations");
        var resumed=DefenseSimulation.LoadJson(a.SaveJson()); a.Run(80); resumed.Run(80);
        check(a.SaveJson()==resumed.SaveJson(),"Defense: save resumes exactly");
        var unseen=new DefenseSimulation(3); unseen.Order("route",2); unseen.Run(85);
        check(unseen.Observe().RaidLane==-1,"Defense: enemy cannot target an unobserved route");
        var seen=new DefenseSimulation(3); seen.Run(65); seen.Order("route",2); seen.Run(20);
        check(seen.Observe().RaidLane==0,"Defense: report retains witnessed lane after player changes route");
        seen.Run(20); check(seen.Observe().Integrity==6,"Defense: rerouting makes stale intelligence miss");
        var hit=new DefenseSimulation(3); hit.Run(105); check(hit.Observe().Integrity==4,"Defense: exposed unguarded route takes damage");
        var guarded=new DefenseSimulation(3); guarded.Order("guard",0); guarded.Run(105);
        check(guarded.Observe().Integrity==6 && guarded.Observe().Evidence==-1,"Defense: intercepted scout cannot send evidence");
        var vote=new DefenseSimulation(3); vote.Run(45); check(vote.Order("vote",0),"Defense: player resolves an open council");
        check(!vote.Order("vote",1),"Defense: council cannot be voted twice");
        vote.Run(60); check(vote.Observe().Integrity==6,"Defense: capture policy interrupts scouting");
        var path=Path.Combine(Path.GetTempPath(),"unseen-defense-"+Guid.NewGuid().ToString("N"));
        var recorded=new DefenseSimulation(3); string session;
        using(var recorder=new PlaytestRecorder(path,recorded,"test","en")) { session=recorder.DirectoryPath; recorded.Run(130); }
        check(PlaytestReplay.Restore(session).StateJson==recorded.SaveJson(),"Defense: detailed playtest reconstructs exact world");
    }
}
