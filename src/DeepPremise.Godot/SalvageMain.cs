using Godot;
using DeepPremise.Core.Defense;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO=System.IO;

namespace DeepPremise.Godot;

public partial class SalvageMain : Control
{
    private SalvageRun run=null!; private RunState state=null!; private PlaytestRecorder recorder=null!;
    private Dictionary<string,string> ko=new(), en=new(); private bool korean=true,paused; private int speed=1;
    private string selected="spark", root="", save="", notice=""; private double elapsed,saveTime,smokeTime; private bool initialized,smoke;
    private Label stats=null!, headline=null!, hint=null!, detail=null!, feed=null!;
    private Button launch=null!, pause=null!; private VBoxContainer rewardBox=null!; private ArenaCanvas arena=null!;
    private Button dispatch=null!,recall=null!;private Label expedition=null!;private OptionButton missions=null!,relics=null!;private HBoxContainer missionRow=null!;
    private readonly Dictionary<string,Button> cards=new(); private string rewardSignature="";
    private int smokeStage;
    private bool smokeExpedition;private double impactPause;private int lastCombo;
    private string T(string key)=>(korean?ko:en).GetValueOrDefault(key,en.GetValueOrDefault(key,key));
    public override void _Ready()
    {
        root=ProjectSettings.GlobalizePath("res://artifacts"); IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save=IO.Path.Combine(OS.GetUserDataDir(),"salvage-v7.json");
        smokeExpedition=OS.GetCmdlineUserArgs().Contains("--smoke-expedition");
        smoke=smokeExpedition||OS.GetCmdlineUserArgs().Contains("--smoke-salvage");
        if(smoke)save=IO.Path.Combine(root,"live","smoke-salvage.json");
        ko=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/salvage.ko.json"))!;
        en=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/salvage.en.json"))!;
        korean=!OS.GetCmdlineUserArgs().Contains("--language=en");
        Theme=new Theme {DefaultFont=GD.Load<Font>("res://game/assets/NotoSansKR.ttf"),DefaultFontSize=17};
        Theme.SetColor("font_color","Label",new Color("e6e9df"));
        var fresh=OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if(fresh&&!smoke&&IO.File.Exists(save))IO.File.Copy(save,save+"."+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".bak");
        run=!smoke&&!fresh&&IO.File.Exists(save)?SalvageRun.LoadJson(IO.File.ReadAllText(save)):SalvageRun.Adventure(7351);
        paused=run.Observe().Phase=="fight";
        StartRecording(); Build(); initialized=true; UpdateView(); Persist();
        if(smoke){foreach(var cell in new[]{4,11,18})Action("place",()=>run.Place(cell,"spark"),new{cell});Action("place",()=>run.Place(5,"oil"));Action("launch",run.Launch);}
        if(smokeExpedition)
        {
            run.Run(2000);run.Choose(run.Observe().Rewards[0]);run.SetMission("relic");run.PlanExpedition();run.Place(10,"spark");run.Launch();UpdateView();
        }
    }
    private void StartRecording()=>recorder=new PlaytestRecorder(IO.Path.Combine(root,"playtests"),run,"0.6.0",korean?"ko":"en");
    private static StyleBoxFlat Box(string color,int radius=10)=>new(){BgColor=new Color(color),CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius,ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=9,ContentMarginBottom=9};
    private Button MakeButton(string text,Action action,Color? accent=null)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,42)};
        b.AddThemeStyleboxOverride("normal",Box("243536")); b.AddThemeStyleboxOverride("hover",Box("354c4b")); b.AddThemeStyleboxOverride("pressed",Box("426059"));
        b.AddThemeColorOverride("font_color",accent??new Color("e6e9df")); b.Pressed+=()=>{action();UpdateView();}; return b;
    }
    private void Build()
    {
        foreach(var child in GetChildren()){RemoveChild(child);child.QueueFree();} cards.Clear();rewardSignature="";
        var bg=new ColorRect{Color=new Color("0e191e"),MouseFilter=MouseFilterEnum.Ignore};bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        var margins=new MarginContainer();margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(margins);
        foreach(var edge in new[]{"left","right","top","bottom"})margins.AddThemeConstantOverride("margin_"+edge,22);
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",12);margins.AddChild(column);
        var top=new HBoxContainer();column.AddChild(top);
        var title=new Label{Text="UNSEEN ORDER  /  "+T("subtitle"),SizeFlagsHorizontal=SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",27);top.AddChild(title);
        top.AddChild(MakeButton("EN / 한국어",()=>{korean=!korean;Build();}));
        top.AddChild(MakeButton(T("restart"),()=>{if(state.Phase=="fight"){notice="finish_first";return;} recorder.Record("action",new{Action="new-run"});recorder.Dispose();run=SalvageRun.Adventure((uint)Random.Shared.Next(1,int.MaxValue));StartRecording();paused=false;notice="";Persist();}));
        stats=new Label();stats.AddThemeFontSizeOverride("font_size",21);column.AddChild(stats);
        headline=new Label();headline.AddThemeColorOverride("font_color",new Color("efbc77"));column.AddChild(headline);
        missionRow=new HBoxContainer();column.AddChild(missionRow);
        missions=new OptionButton();foreach(var m in new[]{"rescue","relic","cache"})missions.AddItem(T("mission_"+m));
        missions.Selected=Array.IndexOf(new[]{"rescue","relic","cache"},run.Observe().Mission);
        missions.ItemSelected+=index=>Action("mission",()=>run.SetMission(new[]{"rescue","relic","cache"}[(int)index]));missionRow.AddChild(missions);
        dispatch=MakeButton(T("dispatch"),()=>{if(state.Phase=="build")Action("plan",run.PlanExpedition);else Action("dispatch",run.Dispatch);});missionRow.AddChild(dispatch);
        recall=MakeButton(T("recall"),()=>Action("recall",run.RecallCourier));missionRow.AddChild(recall);
        relics=new OptionButton();missionRow.AddChild(relics);
        relics.ItemSelected+=index=>Action("equip",()=>run.EquipRelic(index==0?"none":state.Relics[(int)index-1]));
        expedition=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};missionRow.AddChild(expedition);
        var body=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",18);column.AddChild(body);
        var left=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};body.AddChild(left);
        arena=new ArenaCanvas{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(540,330),Read=()=>state,Translate=T,Selection=()=>selected};
        arena.Click=(cell,right)=>Action(right?"sell":"place",()=>right?run.Sell(cell):run.Place(cell,selected),new{cell,selected});
        arena.LanePulse=lane=>Action("pulse",()=>run.Pulse(lane),new{lane});left.AddChild(arena);
        arena.Throw=(id,lane,x)=>Action("sling",()=>run.Sling(id,lane,x),new{id,lane,x});
        arena.Move=(from,to)=>Action("move",()=>run.MoveRig(from,to),new{from,to});
        arena.Grab=id=>Action("grab",()=>run.Grab(id),new{id});arena.Cancel=()=>run.CancelGrab();
        hint=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,46)};left.AddChild(hint);
        var controls=new HBoxContainer();left.AddChild(controls);
        launch=MakeButton(T("launch"),()=>Action("launch",run.Launch));launch.SizeFlagsHorizontal=SizeFlags.ExpandFill;controls.AddChild(launch);
        pause=MakeButton(T("pause"),()=>{paused=!paused;recorder.Record("action",new{Action="pause",paused});});controls.AddChild(pause);
        controls.AddChild(MakeButton("1× / 2×",()=>speed=speed==1?2:1));
        controls.AddChild(MakeButton("F8 • "+T("mark"),Mark));
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(330,0)};body.AddChild(scroll);
        var side=new VBoxContainer{CustomMinimumSize=new Vector2(308,0),SizeFlagsHorizontal=SizeFlags.ExpandFill};side.AddThemeConstantOverride("separation",8);scroll.AddChild(side);
        side.AddChild(new Label{Text=T("equipment")});
        foreach(var kind in SalvageRun.Kinds)
        {
            var k=kind;var b=MakeButton(T(k)+"   "+SalvageRun.Cost(k)+" ◆",()=>{selected=k;notice="";},ArenaCanvas.Tint(k));cards[k]=b;side.AddChild(b);
        }
        detail=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,90)};side.AddChild(detail);
        rewardBox=new VBoxContainer();side.AddChild(rewardBox);
        feed=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};feed.AddThemeColorOverride("font_color",new Color("a5b9b3"));side.AddChild(feed);
    }
    private void Action(string name,Func<bool> act,object? details=null)
    {
        bool ok=act();notice=ok?"":"unavailable";recorder.Record("action",new{Action=name,Accepted=ok,Detail=details});recorder.Capture(name);UpdateView();Persist();
    }
    private void Mark(){recorder.Record("action",new{Action="feedback",Detail=new{Category="moment",Text="",Selected=selected,Paused=paused}});recorder.Capture("feedback");notice="marked";}
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is not InputEventKey{Pressed:true,Echo:false} key)return;
        if(key.Keycode==Key.Space){if(state.Phase=="build")Action("launch",run.Launch);else {paused=!paused;recorder.Record("action",new{Action="pause",paused});}}
        if(key.Keycode==Key.F8)Mark();
        if(key.Keycode>=Key.Key1 && key.Keycode<=Key.Key5){selected=SalvageRun.Kinds[(int)(key.Keycode-Key.Key1)];notice="";}
        if(key.Keycode==Key.Q)Action("pulse",()=>run.Pulse(0));
        if(key.Keycode==Key.W)Action("pulse",()=>run.Pulse(1));
        if(key.Keycode==Key.E)Action("pulse",()=>run.Pulse(2));
        UpdateView();
    }
    public override void _Process(double delta)
    {
        if(!initialized)return;
        if(impactPause>0)impactPause-=delta;else elapsed+=delta*(arena.IsAiming?.35:1);
        saveTime+=delta;
        if(smoke)
        {
            smokeTime+=delta;
            if(!smokeExpedition&&smokeTime>1&&smokeStage==0)
            {
                var target=state.Enemies.FirstOrDefault(e=>e.Oil>0);
                if(target!=null){var p=arena.ScreenPoint(target.X,target.Lane);Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});smokeStage=1;}
            }
            if(!smokeExpedition&&smokeTime>1.4&&smokeStage==1)
            {var p=arena.ScreenPoint(4.2,1);Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p,ButtonMask=MouseButtonMask.Left});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});smokeStage=2;}
            if(smokeExpedition&&smokeTime>4&&smokeStage==0){Action("recall",run.RecallCourier);smokeStage=3;}
            if(smokeTime>11)
            {
                GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",smokeExpedition?"expedition-smoke.png":korean?"salvage-smoke-ko.png":"salvage-smoke-en.png"));
                bool success=smokeExpedition?state.MissionComplete&&state.CargoTier==1:state.Rigs.Any(r=>r.Captured!="none");GD.Print((smokeExpedition?"NATIVE EXTRACTION: ":"NATIVE SLING INPUT: ")+(success?"PASS":"FAIL"));GetTree().Quit(success?0:1);return;
            }
        }
        while(elapsed>=.05){elapsed-=.05;if(!paused)run.Run(speed);}
        UpdateView();arena.QueueRedraw();
        if(saveTime>=3){saveTime=0;Persist();GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","screen.png"));}
    }
    private void UpdateView()
    {
        state=run.Observe();
        if(state.Combo>lastCombo)impactPause=.065;lastCombo=state.Combo;
        missionRow.Visible=state.Wave>1;
        dispatch.Text=T(state.Phase=="build"?(state.ExpeditionPlanned?"plan_cancel":"plan_send"):"dispatch");
        dispatch.Disabled=state.Phase=="build"?state.Scrap<2:state.Phase!="fight"||state.Dispatched||state.WaveTick>100||state.Scrap<2;
        recall.Disabled=state.Phase!="fight"||state.CourierPhase!="outbound";
        missions.Disabled=state.Phase!="build";relics.Disabled=state.Phase!="build";
        missions.Selected=Array.IndexOf(new[]{"rescue","relic","cache"},state.Mission);
        if(relics.ItemCount!=state.Relics.Count+1){relics.Clear();relics.AddItem(T("equip_none"));foreach(var r in state.Relics)relics.AddItem(T("equip_"+r));}
        relics.Selected=state.Relic=="none"?0:state.Relics.IndexOf(state.Relic)+1;
        expedition.Text=state.CourierPhase is "outbound" or "returning"?T("courier")+" "+state.CourierHealth+"♥ · "+T(state.CargoTier==0?"empty_cargo":state.CargoTier==1?"near_prize":"deep_prize"):
            T("crew")+": "+(state.Crew.Count==0?"—":string.Join(" / ",state.Crew.Select(c=>T(c))));
        stats.Text=$"{T("wave")} {state.Wave}/8     ◆ {state.Scrap} {T("scrap")}     ♥ {state.Hull}/18     {T("kills")} {state.Kills}     {T("combos")} {state.Combo}";
        headline.Text=T(state.Phase)+"   /   "+T(state.Weather)+"   /   "+T(state.Adaptation)+(paused?"   •   "+T("paused"):"");
        hint.Text=notice!=""?T(notice):T(state.Phase=="build"?"build_hint":state.Phase=="fight"?"fight_hint":state.Phase=="reward"?"reward_hint":state.Phase=="won"?"win_hint":"loss_hint");
        detail.Text=T(selected+"_desc")+"\n\n"+(state.Relic=="none"?T("upgrade_hint"):T(state.Relic+"_law"));
        var hoveredRig=state.Rigs.FirstOrDefault(r=>r.Cell==arena.HoverCell);
        var hoveredEnemy=state.Enemies.FirstOrDefault(e=>e.Id==arena.HoverEnemyId);
        if(hoveredRig?.Captured is not (null or "none"))detail.Text=T("trait_"+hoveredRig.Captured)+$"  Lv.{hoveredRig.Growth}\n"+T("trait_"+hoveredRig.Captured+"_desc")+"\n\n"+T("feeding_hint")+$"\n{T("escape_in")} {hoveredRig.Satiation/20.0:F1}s";
        if(hoveredEnemy!=null)detail.Text=T("enemy_"+hoveredEnemy.Kind)+$"  {Math.Ceiling(hoveredEnemy.Health)}/{Math.Ceiling(hoveredEnemy.MaxHealth)} ♥\n\n"+T("trait_"+hoveredEnemy.Kind+"_desc")+"\n\n"+T("drag_hint");
        launch.Disabled=state.Phase!="build" || !state.Rigs.Any(r=>r.Kind is "spark" or "flame" or "shifter");launch.Text=T("launch");pause.Text=T(paused?"resume":"pause");
        foreach(var (kind,button) in cards)button.Modulate=kind==selected?Colors.White:new Color(.65f,.7f,.72f);
        var signature=state.Phase+string.Join(",",state.Rewards);
        if(signature!=rewardSignature)
        {
            rewardSignature=signature;foreach(var child in rewardBox.GetChildren()){rewardBox.RemoveChild(child);child.QueueFree();}
            if(state.Phase=="reward")
            {
                rewardBox.AddChild(new Label{Text=T("choose_reward")});
                foreach(var reward in state.Rewards){var r=reward;var b=MakeButton(T("reward_"+r),()=>Action("reward",()=>run.Choose(r),new{Reward=r}),new Color("efbc77"));b.TooltipText=T("reward_"+r+"_desc");rewardBox.AddChild(b);}
            }
        }
        feed.Text=T("field")+"\n"+string.Join("\n",state.History.AsEnumerable().Reverse().Distinct().Select(T));
    }
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);IO.File.WriteAllText(save+".tmp",run.SaveJson());IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live","context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="0.6.0",run.Tick,Paused=paused,Session=recorder.DirectoryPath,State=run.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree(){if(initialized){Persist();recorder.Dispose();}}
}

public partial class ArenaCanvas : Control
{
    public Func<RunState> Read{get;set;}=null!;public Func<string,string> Translate{get;set;}=x=>x;public Func<string> Selection{get;set;}=()=>"spark";
    public Action<int,bool>? Click{get;set;} public Action<int>? LanePulse{get;set;}
    public Action<int,int,double>? Throw{get;set;} public Action<int>? Grab{get;set;} public Action? Cancel{get;set;}
    public Action<int,int>? Move{get;set;}private int moving=-1;
    private int held=-1;private Vector2 cursor;public bool IsAiming=>held>=0;
    public int HoverCell=>hover;
    public int HoverEnemyId=>Read()?.Enemies.FirstOrDefault(e=>Point(e.X,e.Lane).DistanceTo(cursor)<25)?.Id??-1;
    private int hover=-1;private double clock;
    public static Color Tint(string kind)=>new(kind switch{"spark"=>"75d4ec","oil"=>"b3a1eb","flame"=>"f5a061","shifter"=>"8ce1a7","collector"=>"e8cf7a",_=>"d9e8e3"});
    private float Cw=>(Size.X-130)/7;private float Ch=>(Size.Y-110)/3;
    private Vector2 Point(double x,int lane)=>new(75+(float)x*Cw,65+(lane+.5f)*Ch);
    public Vector2 ScreenPoint(double x,int lane)=>GlobalPosition+Point(x,lane);
    public override void _Process(double delta){clock+=delta;if(held>=0&&Read()?.HeldEnemy!=held)held=-1;QueueRedraw();}
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton{Pressed:false,ButtonIndex:MouseButton.Left} drop&&moving>=0)
        {
            int col=(int)Math.Floor((drop.Position.X-75)/Cw),lane=(int)Math.Floor((drop.Position.Y-65)/Ch);
            if(col>=0&&col<7&&lane>=0&&lane<3){var target=lane*7+col;if(target==moving)Click?.Invoke(target,false);else Move?.Invoke(moving,target);}
            moving=-1;return;
        }
        if(e is InputEventMouseMotion motion){var p=motion.Position;cursor=p;int col=(int)Math.Floor((p.X-75)/Cw);int lane=(int)Math.Floor((p.Y-65)/Ch);hover=col>=0&&col<7&&lane>=0&&lane<3?lane*7+col:-1;}
        if(e is InputEventMouseButton{Pressed:false,ButtonIndex:MouseButton.Left} released&&held>=0)
        {int lane=(int)Math.Floor((released.Position.Y-65)/Ch);if(lane>=0&&lane<3)Throw?.Invoke(held,lane,(released.Position.X-75)/Cw);else Cancel?.Invoke();held=-1;return;}
        if(e is InputEventMouseButton{Pressed:true} mouse)
        {
            if(Read().Phase=="fight")
            {
                var target=Read().Enemies.Where(x=>x.Flight==0).OrderBy(x=>Point(x.X,x.Lane).DistanceTo(mouse.Position)).FirstOrDefault();
                if(mouse.ButtonIndex==MouseButton.Left&&target!=null&&Point(target.X,target.Lane).DistanceTo(mouse.Position)<30&&Read().SlingCooldown==0){Grab?.Invoke(target.Id);held=target.Id;cursor=mouse.Position;}
                else {int lane=(int)Math.Floor((mouse.Position.Y-65)/Ch);if(lane>=0&&lane<3)LanePulse?.Invoke(lane);}
            }
            else
            {
                int col=(int)Math.Floor((mouse.Position.X-75)/Cw);int lane=(int)Math.Floor((mouse.Position.Y-65)/Ch);
                if(col>=0&&col<7&&lane>=0&&lane<3)
                {int cell=lane*7+col;if(mouse.ButtonIndex==MouseButton.Left&&Read().Rigs.Any(r=>r.Cell==cell))moving=cell;else Click?.Invoke(cell,mouse.ButtonIndex==MouseButton.Right);}
            }
        }
    }
    private void Text(Vector2 at,string text,int size,Color color)=>DrawString(GetThemeDefaultFont(),at,text,HorizontalAlignment.Left,-1,size,color);
    public override void _Draw()
    {
        var s=Read();if(s==null)return;
        DrawStyleBox(new StyleBoxFlat{BgColor=new Color("172a30"),CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14},new Rect2(Vector2.Zero,Size));
        Text(new Vector2(20,32),Translate("arena_title"),18,new Color("b0c9c3"));
        Text(new Vector2(Size.X-190,32),Translate("incoming")+" ←",16,new Color("ee9984"));
        for(int i=0;i<21;i++)
        {
            int lane=i/7,col=i%7;var pos=new Vector2(75+col*Cw,65+lane*Ch);var rect=new Rect2(pos+Vector2.One*3,new Vector2(Cw-6,Ch-6));
            DrawStyleBox(new StyleBoxFlat{BgColor=new Color((col+lane)%2==0?"21373b":"1d3237"),CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7},rect);
            if(i==hover&&s.Phase=="build")DrawRect(rect,Tint(Selection()),false,2);
            if(s.Phase=="build" && s.Rigs.Count==0 && (i==5 || i==12 || i==19))Text(pos+new Vector2(Cw*.3f,Ch*.55f),"+",32,new Color("68877e"));
        }
        for(int lane=0;lane<3;lane++)
        {
            var p=Point(-.25,lane);DrawCircle(p,18,new Color("38524e"));Text(p+new Vector2(-7,6),new[]{"Q","W","E"}[lane],18,s.Pulse==0?new Color("98e9b4"):new Color("607570"));
            DrawLine(Point(0,lane)+new Vector2(0,Ch*.45f),Point(7,lane)+new Vector2(0,Ch*.45f),new Color("335056"),1);
        }
        if(s.Phase=="build" && hover>=0)
        {
            var rig=s.Rigs.FirstOrDefault(r=>r.Cell==hover);var kind=rig?.Kind??Selection();
            var center=Point(hover%7+.5,hover/7);float reach=(kind is "oil" or "shifter"?1f:1.7f+s.Range*.35f)*Cw;
            if(kind!="collector")DrawRect(new Rect2(Math.Max(75,center.X-reach),center.Y-32,Math.Min(Size.X-55,center.X+reach)-Math.Max(75,center.X-reach),64),new Color(Tint(kind),.12f));
            var cost=rig==null?SalvageRun.Cost(kind):3+rig.Level*2;
            Text(new Vector2(20,53),Translate(kind)+" • "+(rig?.Level==3?"MAX":$"{cost} ◆"),15,Tint(kind));
        }
        foreach(var r in s.Rigs)
        {
            var p=Point(r.Cell%7+.5,r.Cell/7);var color=Tint(r.Kind);
            if(r.Captured=="sprinter")foreach(var other in s.Rigs.Where(o=>o.Cell!=r.Cell&&o.Cell/7==r.Cell/7&&Math.Abs(o.Cell%7-r.Cell%7)<=2))DrawLine(p,Point(other.Cell%7+.5,other.Cell/7),new Color(.55f,.9f,.65f,.35f),2);
            DrawCircle(p+new Vector2(3,7),23,new Color(0,0,0,.2f));DrawCircle(p,22,new Color("30474a"));DrawArc(p,24,0,Mathf.Tau,32,color,2);
            if(r.Kind=="spark")DrawPolyline(new[]{p+new Vector2(5,-16),p+new Vector2(-8,1),p+new Vector2(5,1),p+new Vector2(-5,16)},color,4,true);
            if(r.Kind=="oil"){DrawCircle(p+new Vector2(0,4),10,color);DrawPolygon(new[]{p+new Vector2(-8,0),p+new Vector2(0,-16),p+new Vector2(8,0)},new[]{color});}
            if(r.Kind=="flame")DrawPolygon(new[]{p+new Vector2(-11,11),p+new Vector2(-7,-6),p+new Vector2(0,0),p+new Vector2(7,-17),p+new Vector2(12,11)},new[]{color});
            if(r.Kind=="shifter")DrawPolyline(new[]{p+new Vector2(-12,-9),p+new Vector2(10,10),p+new Vector2(10,-4),p+new Vector2(10,10),p+new Vector2(-3,10)},color,3,true);
            if(r.Kind=="collector"){DrawRect(new Rect2(p-new Vector2(11,10),new Vector2(22,20)),color,false,3);Text(p+new Vector2(-5,6),"+",18,color);}
            if(r.Captured!="none")
            {
                DrawCircle(p,8,new Color(r.Captured=="brute"?"df7fa0":r.Captured=="armored"?"a6b5c3":"e7967c"));
                DrawCircle(p+new Vector2(-2,-2),2,new Color("14292b"));
                Text(p+new Vector2(-Cw*.4f,51),Translate("trait_"+r.Captured),12,color);
                DrawRect(new Rect2(p.X-24,p.Y-38,48,5),new Color("4c3436"));DrawRect(new Rect2(p.X-24,p.Y-38,48*Math.Clamp(r.Satiation/360f,0,1),5),r.Satiation<80?new Color("f47e66"):new Color("a5d582"));
                Text(p+new Vector2(23,-25),$"{r.Growth}★",13,color);
                if(r.Satiation<80)Text(p+new Vector2(-22,-49),Translate("feed_now"),14,new Color("f47e66"));
                if(r.Captured=="sprinter")DrawArc(p,31+(float)Math.Sin(clock*4)*3,0,Mathf.Tau,32,color,1);
            }
            for(int j=0;j<r.Level;j++)DrawCircle(p+new Vector2((j-(r.Level-1)*.5f)*9,32),3,color);
        }
        foreach(var enemy in s.Enemies)
        {
            var p=Point(enemy.X,enemy.Lane);p.Y+=(float)Math.Sin(clock*9+enemy.Id)*3;float radius=enemy.Kind=="brute"?21:enemy.Kind=="armored"?15:12;
            if(enemy.Flight>0)
            {
                float travel=Math.Clamp((24-enemy.Flight)/8f,0,1);p.Y=Mathf.Lerp(Point(enemy.X,enemy.FromLane).Y,p.Y,travel)-(float)Math.Sin(travel*Math.PI)*35;
                DrawLine(p-new Vector2(45,0),p,new Color("e8cf7a"),5);
            }
            var color=new Color(enemy.Kind=="armored"?"a6b5c3":enemy.Kind=="brute"?"df7fa0":"e7967c");
            DrawCircle(p+new Vector2(3,6),radius,new Color(0,0,0,.3f));DrawPolygon(new[]{p+new Vector2(-radius,0),p+new Vector2(0,-radius),p+new Vector2(radius,0),p+new Vector2(0,radius)},new[]{color});
            DrawCircle(p+new Vector2(-4,-3),2,new Color("18272b"));
            if(enemy.Escaped){DrawArc(p,radius+7,0,Mathf.Tau,24,new Color("ff596a"),3);Text(p+new Vector2(-20,-radius-22),Translate("escaped"),13,new Color("ff8996"));}
            if(enemy.Kind=="armored"&&!enemy.Cracked)DrawArc(p,radius+4,Mathf.Pi*.6f,Mathf.Pi*1.4f,16,new Color("d1e1ef"),4);
            if(enemy.Oil>0)DrawArc(p,radius+5,0,Mathf.Tau,20,Tint("oil"),3);
            if(enemy.Burn>0)DrawArc(p,radius+7,0,Mathf.Tau,20,Tint("flame"),3);
            DrawRect(new Rect2(p.X-15,p.Y-radius-11,30,4),new Color("392e33"));DrawRect(new Rect2(p.X-15,p.Y-radius-11,30*(float)Math.Max(0,enemy.Health/enemy.MaxHealth),4),new Color("a0e1a7"));
        }
        if(held>=0)
        {
            var grabbed=s.Enemies.FirstOrDefault(e=>e.Id==held);
            if(grabbed!=null)
            {
                DrawLine(Point(grabbed.X,grabbed.Lane),cursor,new Color("e8cf7a"),3);DrawArc(cursor,25,0,Mathf.Tau,24,new Color("e8cf7a"),2);
                DrawLine(cursor,cursor+new Vector2(Cw*1.7f,0),new Color(1,.85f,.4f,.65f),12);
                Text(cursor+new Vector2(-45,-35),Translate("release_throw"),18,new Color("e8cf7a"));
            }
        }
        if(moving>=0){DrawCircle(cursor,24,new Color(1,.9f,.6f,.4f));DrawLine(Point(moving%7+.5,moving/7),cursor,new Color("e8cf7a"),2);}
        if(s.Wave>1&&(s.Phase=="build"||(!s.MissionComplete&&s.WaveTick<=220)))
        {
            var p=Point(6.6,s.MissionLane)+new Vector2(0,-Ch*.3f);DrawRect(new Rect2(p-new Vector2(13,13),new Vector2(26,26)),new Color("e8cf7a"),false,3);
            Text(p+new Vector2(-60,-22),Translate("mission_"+s.Mission),15,new Color("e8cf7a"));
            var near=Point(3.2,s.MissionLane)+new Vector2(0,-Ch*.3f);DrawCircle(near,8,new Color("e8cf7a"));Text(near+new Vector2(-30,-20),Translate("near_prize"),13,new Color("e8cf7a"));
        }
        if(s.CourierPhase is "outbound" or "returning")
        {
            var p=Point(s.CourierX,s.MissionLane)+new Vector2(0,Ch*.28f);DrawCircle(p,11,new Color("f5f2c2"));
            DrawLine(p,Point(s.CourierPhase=="outbound"?6.6:0,s.MissionLane)+new Vector2(0,Ch*.28f),new Color(1,1,.7f,.2f),2);
            Text(p+new Vector2(-32,28),Translate("courier")+" "+s.CourierHealth+"♥",14,new Color("f5f2c2"));
            if(s.Cargo)DrawRect(new Rect2(p+new Vector2(12,-6),new Vector2(10,10)),new Color("e8cf7a"));
        }
        foreach(var impact in s.Impacts)
        {
            var p=Point(impact.X,impact.Lane);var color=Tint(impact.Kind);float t=(float)Math.Clamp((impact.Until-s.Tick)/8.0,0,1);
            if(impact.Kind is "spark" or "flame")DrawLine(Point(impact.From,impact.Lane),p,color,impact.Kind=="flame"?5:2,true);
            if(impact.Kind is "blast" or "kill" or "pulse")DrawArc(p,12+(1-t)*35,0,Mathf.Tau,24,new Color(1,.7f,.35f,t),3);
            if(impact.Damage>0)Text(p+new Vector2(8,-18-(1-t)*25),$"{impact.Damage:0.#}",impact.Kind=="blast"?23:16,new Color(1,.9f,.7f,t));
            if(impact.Kind=="income")Text(p+new Vector2(-12,-25),"+◆",20,Tint("collector"));
        }
        string bottom=s.Phase=="fight" ? Translate("pulse")+"  "+(s.Pulse==0?Translate("ready"):$"{s.Pulse/20.0:F1}s")+"   •   "+Translate("remaining")+" "+(s.Pending+s.Enemies.Count) : Translate("grid_hint");
        Text(new Vector2(20,Size.Y-18),bottom+"  /  "+Translate("sling")+" "+(s.SlingCooldown==0?Translate("ready"):$"{s.SlingCooldown/20.0:F1}s"),16,new Color("b0c9c3"));
        for(int i=0;i<18;i++)DrawRect(new Rect2(9,65+i*(Size.Y-140)/18,6,(Size.Y-140)/18-3),new Color(i<s.Hull?"8ce1a7":"493e43"));
        if(s.Phase is "reward" or "won" or "lost")
        {
            var rect=new Rect2(50,Size.Y*.38f,Size.X-100,105);DrawStyleBox(new StyleBoxFlat{BgColor=new Color(.055f,.1f,.12f,.95f),CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12},rect);
            Text(rect.Position+new Vector2(24,43),Translate(s.Phase=="reward"?"clear_title":s.Phase),30,new Color("edcc88"));
            Text(rect.Position+new Vector2(24,78),Translate(s.Phase=="reward"?"reward_hint":s.Phase=="won"?"win_hint":"loss_hint"),16,new Color("dbe3d5"));
        }
    }
}
