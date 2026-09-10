namespace DeepPremise.Core.Defense;

public sealed partial class SalvageRun
{
    public bool MoveRig(int from,int to)
    {
        var rig=s.Rigs.FirstOrDefault(r=>r.Cell==from);
        if(s.Phase!="build"||to is <0 or >=21||rig==null||s.Rigs.Any(r=>r.Cell==to))return false;
        rig.Cell=to;Log("machine_moved",new{from,to,rig.Captured});return true;
    }
    public bool PlanExpedition()
    {
        if(s.Phase!="build")return false;
        s.ExpeditionPlanned=!s.ExpeditionPlanned;Log("expedition_plan",new{s.ExpeditionPlanned});return true;
    }
    public bool SetMission(string mission)
    {
        if(s.Phase!="build" || mission is not ("rescue" or "relic" or "cache"))return false;
        s.Mission=mission;Log("mission_"+mission,new{s.MissionLane});return true;
    }
    public bool EquipRelic(string relic)
    {
        if(s.Phase!="build" || (relic!="none"&&!s.Relics.Contains(relic)))return false;
        s.Relic=relic;Log("equip_"+relic);return true;
    }
    public bool Dispatch()
    {
        if(s.Phase!="fight"||s.Dispatched||s.Scrap<2||s.WaveTick>100)return false;
        s.Scrap-=2;s.Dispatched=true;s.CourierPhase="outbound";Log("dispatched",new{s.Mission,s.MissionLane});return true;
    }
    public bool RecallCourier()
    {
        if(s.Phase!="fight"||s.CourierPhase!="outbound")return false;
        s.CourierPhase="returning";Log("courier_recalled");return true;
    }
    private void ExpeditionStep()
    {
        s.WaveTick++;if(s.SlingCooldown>0)s.SlingCooldown--;
        if(s.HeldEnemy>=0&&--s.HoldTicks<=0){s.HeldEnemy=-1;s.SlingCooldown=40;Log("grab_expired");}
        if(s.Debt>0&&s.Tick%75==0)
        {
            s.Pending+=s.Debt;Log("debt_collected",new{s.Debt});s.Debt=0;
        }
        if(s.CourierPhase is not ("outbound" or "returning"))return;
        s.CourierX+=s.CourierPhase=="outbound"?.055:-.065;
        if(s.CourierPhase=="outbound"&&s.WaveTick>220){s.CourierPhase="returning";Log("site_collapsed");}
        if(s.CourierPhase=="outbound"&&s.CourierX>=3.2&&s.CargoTier==0){s.CargoTier=1;s.Cargo=true;Log("partial_cargo");}
        if(s.Tick%16==0&&s.Enemies.Any(e=>e.Health>0&&e.Lane==s.MissionLane&&Math.Abs(e.X-s.CourierX)<.65))
        {
            s.CourierHealth--;Log("courier_hit",new{s.CourierHealth,s.CourierX});
            if(s.CourierHealth<=0){s.CourierPhase="lost";s.Cargo=false;Log("courier_lost");return;}
        }
        if(s.CourierPhase=="outbound"&&s.CourierX>=6.6)
        {s.Cargo=true;s.CargoTier=2;s.CourierPhase="returning";Log("cargo_acquired",new{s.Mission});}
        if(s.CourierPhase=="returning"&&s.CourierX<=0)
        {
            s.CourierX=0;s.CourierPhase="home";
            if(!s.Cargo){Log("courier_safe");return;}
            s.MissionComplete=true;s.Cargo=false;
            if(s.CargoTier==1){s.Scrap+=5;Log("partial_banked");return;}
            switch(s.Mission)
            {
                case "cache":s.Scrap+=14;Log("cache_recovered");break;
                case "rescue":
                    var person=new[]{"engineer","scout","medic"}.FirstOrDefault(p=>!s.Crew.Contains(p));
                    if(person!=null){s.Crew.Add(person);Log("rescued_"+person);}else{s.Scrap+=8;Log("refugees_safe");}
                    if(s.Crew.Contains("medic"))s.Hull=Math.Min(18,s.Hull+3);
                    break;
                case "relic":
                    var available=new[]{"inversion","debt","relay"}.Where(r=>!s.Relics.Contains(r)).ToArray();
                    if(available.Length>0){var relic=available[Next(available.Length)];s.Relics.Add(relic);Log("found_"+relic);}else{s.Scrap+=10;Log("cache_recovered");}
                    break;
            }
            if(s.Crew.Contains("medic")&&s.Mission!="rescue")s.Hull=Math.Min(18,s.Hull+3);
        }
    }
    public bool Grab(int id)
    {
        if(s.Phase!="fight"||s.SlingCooldown>0||s.HeldEnemy>=0||!s.Enemies.Any(e=>e.Id==id&&e.Health>0&&e.Flight==0))return false;
        s.HeldEnemy=id;s.HoldTicks=60;Log("grabbed",new{id});return true;
    }
    public void CancelGrab(){if(s.HeldEnemy>=0){s.HeldEnemy=-1;s.HoldTicks=0;s.SlingCooldown=40;Log("grab_cancelled");}}
    public bool Sling(int id,int lane,double? landingX=null)
    {
        var target=s.Enemies.FirstOrDefault(e=>e.Id==id&&e.Health>0&&e.Flight==0);
        if(s.Phase!="fight"||s.SlingCooldown>0||lane is <0 or >2||target==null)return false;
        s.HeldEnemy=-1;s.HoldTicks=0;
        s.SlingCooldown=75;target.FromLane=target.Lane;target.FromX=target.X;target.LandingX=Math.Clamp(landingX??target.X,.5,6.5);target.Lane=lane;target.Flight=24;target.Collided.Clear();
        Log("slung",new{id,lane,target.Oil,target.Burn,target.Kind});return true;
    }
    private void FlightStep(Intruder ball)
    {
        ball.Flight--;
        if(ball.Flight>=16){ball.X=ball.FromX+(ball.LandingX-ball.FromX)*(24-ball.Flight)/8.0;return;}
        if(ball.Kind=="armored"&&!ball.Cracked){ball.Cracked=true;Log("shell_cracked");}
        ball.X=Math.Min(7.8,ball.X+.13);
        foreach(var other in s.Enemies.Where(e=>e.Id!=ball.Id&&e.Health>0&&e.Lane==ball.Lane&&Math.Abs(e.X-ball.X)<.8&&!ball.Collided.Contains(e.Id)).ToArray())
        {
            ball.Collided.Add(other.Id);other.X+=.45;other.Slow=20;other.Oil=Math.Max(other.Oil,ball.Oil);other.Burn=Math.Max(other.Burn,ball.Burn);
            Hit(other,ball.Kind=="brute"?10:5,"blast",ball.X);s.Combo++;Log("bowling",new{ball.Id,Target=other.Id});
        }
        var magnet=s.Rigs.FirstOrDefault(r=>r.Kind=="collector"&&r.Cell/7==ball.Lane&&Math.Abs(r.Cell%7+.5-ball.X)<.45);
        if(magnet!=null)
        {
            ball.Health=0;if(!ball.Escaped)s.Scrap+=3+magnet.Level;
            bool feeding=magnet.Captured==ball.Kind;
            magnet.Growth=feeding?Math.Min(3,magnet.Growth+1):1;
            magnet.Captured=ball.Kind;magnet.Satiation=ball.Escaped?140:360;
            magnet.Shield=ball.Kind=="armored"?magnet.Growth:0;magnet.Cooldown=0;
            Log(feeding?"creature_fed":"grafted_"+ball.Kind,new{magnet.Cell,magnet.Growth,magnet.Satiation});
        }
    }
    private void LivingMachinesStep()
    {
        foreach(var rig in s.Rigs.Where(r=>r.Captured!="none"))
        {
            rig.Satiation--;
            if(rig.Satiation==80)Log("creature_hungry",new{rig.Cell,rig.Captured});
            if(rig.Satiation>0)continue;
            double health=(10+s.Wave*3)*rig.Growth;
            s.Enemies.Add(new(){Id=++s.NextId,Kind=rig.Captured,Lane=rig.Cell/7,X=rig.Cell%7+.5,Health=health,MaxHealth=health,Escaped=true});
            Log("creature_escaped",new{rig.Cell,rig.Captured,rig.Growth});
            rig.Captured="none";rig.Shield=0;rig.Growth=1;rig.Cooldown=100;
        }
    }
}
