using Godot;
using DeepPremise.Core.Habits;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO=System.IO;

namespace DeepPremise.Godot;

public partial class HabitMain : Control
{
    private HabitWorld world=null!;private HabitState state=null!;private PlaytestRecorder recorder=null!;
    private Dictionary<string,string> en=new(),ko=new();private bool korean=true,paused,smoke;private int speed=1,person=0,site=0,smokeStage;
    private string root="",save="",message="";private double elapsed,saveTime,smokeTime;
    private Label stock=null!,goal=null!,personInfo=null!,siteInfo=null!,journal=null!,toast=null!;
    private Button method=null!,practice=null!,admit=null!;private HabitCanvas map=null!;
    private VBoxContainer people=null!;private int peopleCount=-1;
    private string T(string key)=>(korean?ko:en).GetValueOrDefault(key,key);
    public override void _Ready()
    {
        en=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/habits.en.json"))!;
        ko=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/habits.ko.json"))!;
        korean=!OS.GetCmdlineUserArgs().Contains("--language=en");smoke=OS.GetCmdlineUserArgs().Contains("--smoke-habits");
        root=ProjectSettings.GlobalizePath("res://artifacts");IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save=smoke?IO.Path.Combine(root,"live","habits-smoke.json"):IO.Path.Combine(OS.GetUserDataDir(),"habits-v8.json");
        bool fresh=OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if(fresh&&!smoke&&IO.File.Exists(save))IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        world=!smoke&&!fresh&&IO.File.Exists(save)?HabitWorld.LoadJson(IO.File.ReadAllText(save)):new HabitWorld(17);
        Theme=new Theme{DefaultFont=new FontVariation{BaseFont=GD.Load<Font>("res://game/assets/NotoSansKR.ttf"),VariationEmbolden=.65f},DefaultFontSize=18};
        Theme.SetColor("font_color","Label",new Color("f7f7ee"));
        Theme.SetColor("font_color","Button",new Color("ffffff"));
        Theme.SetColor("font_hover_color","Button",new Color("ffffff"));
        Theme.SetColor("font_pressed_color","Button",new Color("fff1af"));
        Record();Build();Refresh();
        if(smoke){world.Run(470);Refresh();}
        Persist();
    }
    private void Record()=>recorder=new PlaytestRecorder(IO.Path.Combine(root,"playtests"),world,"0.7.0",korean?"ko":"en");
    private Button Button(string text,Action action)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,44)};
        b.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color("34443d"),CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8,ContentMarginLeft=10,ContentMarginRight=10});
        b.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new Color("536353"),CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
        b.Pressed+=()=>{action();Refresh();};return b;
    }
    private void Build()
    {
        foreach(var c in GetChildren()){RemoveChild(c);c.QueueFree();}peopleCount=-1;
        var bg=new ColorRect{Color=new Color("17261f"),MouseFilter=MouseFilterEnum.Ignore};bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        var margin=new MarginContainer();margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(margin);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,20);
        var col=new VBoxContainer();col.AddThemeConstantOverride("separation",12);margin.AddChild(col);
        var bar=new HBoxContainer();col.AddChild(bar);var title=new Label{Text="UNSEEN ORDER  /  "+T("title"),SizeFlagsHorizontal=SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",25);bar.AddChild(title);
        bar.AddChild(Button(T("pause"),TogglePause));bar.AddChild(Button("1× / 3×",()=>{speed=speed==1?3:1;recorder.Record("action",new{Action="speed",speed});}));
        bar.AddChild(Button("EN / 한국어",()=>{korean=!korean;Build();}));bar.AddChild(Button(T("new_world"),()=>{recorder.Dispose();world=new HabitWorld((uint)Random.Shared.Next(1,100000));person=0;site=0;paused=false;message="";Record();Persist();}));
        stock=new Label();stock.AddThemeFontSizeOverride("font_size",24);col.AddChild(stock);
        goal=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};goal.AddThemeColorOverride("font_color",new Color("e5ce8f"));col.AddChild(goal);
        var body=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",18);col.AddChild(body);
        map=new HabitCanvas{Read=()=>state,Translate=T,SelectedPerson=()=>person,SelectedSite=()=>site,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(600,450)};
        map.SelectPerson=id=>{person=id;site=state.People.First(p=>p.Id==id).Job;Refresh();};
        map.SelectSite=id=>{site=id;Refresh();};
        map.Assign=(id,target)=>Act("assign",()=>world.Assign(id,target),new{id,target});body.AddChild(map);
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(380,0)};body.AddChild(scroll);
        var side=new VBoxContainer{CustomMinimumSize=new Vector2(358,0),SizeFlagsHorizontal=SizeFlags.ExpandFill};side.AddThemeConstantOverride("separation",12);scroll.AddChild(side);
        side.AddChild(new Label{Text=T("people")});people=new VBoxContainer();side.AddChild(people);
        personInfo=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};side.AddChild(personInfo);
        side.AddChild(new HSeparator());siteInfo=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};side.AddChild(siteInfo);
        side.AddChild(Button(T("assign_selected"),()=>Act("assign",()=>world.Assign(person,site),new{person,site})));
        method=Button(T("together"),()=>Act("method",()=>world.ToggleTogether(site),new{site}));side.AddChild(method);
        practice=Button(T("practice"),()=>Act("practice",()=>world.TogglePractice(site),new{site}));side.AddChild(practice);
        admit=Button(T("invite"),()=>Act("invite",world.Admit));side.AddChild(admit);
        side.AddChild(new HSeparator());side.AddChild(new Label{Text=T("notebook")});journal=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};journal.AddThemeColorOverride("font_color",new Color("c5d0ba"));side.AddChild(journal);
        toast=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(toast);
    }
    private void Act(string action,Func<bool> execute,object? details=null)
    {
        bool accepted=execute();recorder.Record("action",new{Action=action,Accepted=accepted,Detail=details});recorder.Capture(action);message=accepted?"":"unavailable";Refresh();Persist();
    }
    private void TogglePause(){paused=!paused;recorder.Record("action",new{Action="pause",paused});}
    private void Refresh()
    {
        state=world.Observe();
        if(peopleCount!=state.People.Count)
        {
            peopleCount=state.People.Count;foreach(var c in people.GetChildren()){people.RemoveChild(c);c.QueueFree();}
            foreach(var p in state.People){int id=p.Id;people.AddChild(Button(T(p.Name),()=>{person=id;site=state.People.First(p=>p.Id==id).Job;}));}
        }
        for(int i=0;i<state.People.Count;i++){var p=state.People[i];var b=(Button)people.GetChild(i);b.Text=(person==p.Id?"▶ ":"")+T(p.Name)+"  ·  "+(p.NextJob>=0?"→ ":"")+T(state.Sites[p.NextJob>=0?p.NextJob:p.Job].Name);b.SelfModulate=person==p.Id?new Color("fff0b0"):Colors.White;}
        stock.Text=string.Format(T("day_format"),state.Day)+"   ·   "+string.Join("   ",state.Stock.Select(p=>T(p.Key)+" "+p.Value))+"   ·   "+string.Format(T("population_format"),state.People.Count)+(paused?"  ⏸":"");
        goal.Text=state.Failed?T("dispersed"):state.Established?T("established"):state.GuestWaiting?T("guest_waiting"):state.Strain>0?T("hungry_day"):T("goal");
        var selected=state.People.FirstOrDefault(p=>p.Id==person)??state.People[0];
        personInfo.Text=T(selected.Name)+" — "+T(selected.Thought)+"\n"+T("witnessed")+": "+(selected.Knowledge.Count==0?T("nothing_yet"):string.Join(" / ",selected.Knowledge.Select(k=>T("known_"+k))));
        var place=state.Sites[site];
        siteInfo.Text=T(place.Name)+" · "+T(place.Resource)+"\n"+T("assigned")+": "+string.Join(", ",state.People.Where(p=>p.Job==site).Select(p=>T(p.Name)))+
            (place.Bound?"\n"+T("pair_observation"):"")+(place.Claimed?"\n"+string.Format(T("maker_observation"),T(state.People.First(p=>p.Id==place.Maker).Name)):"")+(place.EchoTrips>0?"\n"+T("echo_observation"):"");
        method.Text=T(place.Together?"together":"separately");method.Visible=site!=3;
        practice.Visible=place.PairSeen||place.ClaimSeen;practice.Text=T(place.Practice?"stop_practice":"practice");
        admit.Visible=state.GuestWaiting;admit.Disabled=state.Stock["cloth"]<3;
        journal.Text=string.Join("\n\n",state.Journal.TakeLast(4).Reverse().Select(e=>
            ((e.Place!=""||e.Person!="")?string.Join(" · ",new[]{e.Place,e.Person}.Where(x=>x!="").Select(T))+"\n":"")+T(e.Key)));
        toast.Text=message!=""?T(message):T("controls");
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is not InputEventKey{Pressed:true,Echo:false} key)return;
        if(key.Keycode==Key.Space)TogglePause();
        if(key.Keycode==Key.F8){recorder.Record("action",new{Action="feedback",Detail=new{Category="moment",Text="",person,site}});recorder.Capture("feedback");message="marked";}
        Refresh();
    }
    public override void _Process(double delta)
    {
        elapsed+=delta;saveTime+=delta;
        while(elapsed>=.1){elapsed-=.1;if(!paused)world.Run(speed);}
        Refresh();
        if(smoke)
        {
            smokeTime+=delta;
            if(smokeTime>1&&smokeStage==0){var p=map.PersonScreen(0);Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});smokeStage=1;}
            if(smokeTime>1.3&&smokeStage==1){var p=map.SiteScreen(1);Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p,ButtonMask=MouseButtonMask.Left});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});smokeStage=2;}
            if(smokeTime>7){bool ok=state.Sites[0].EchoSeen&&state.Sites[0].PairSeen&&state.Sites[3].ClaimSeen;GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"habits-ko.png":"habits-en.png"));GD.Print("NATIVE HABIT REASSIGNMENT: "+(ok?"PASS":"FAIL"));GetTree().Quit(ok?0:1);}
        }
        if(saveTime>=3){saveTime=0;Persist();GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","screen.png"));}
        map.QueueRedraw();
    }
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);IO.File.WriteAllText(save+".tmp",world.SaveJson());IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live","context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="0.7.0",world.Tick,Paused=paused,Session=recorder.DirectoryPath,State=world.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree(){Persist();recorder.Dispose();}
}

public partial class HabitCanvas : Control
{
    public Func<HabitState> Read{get;set;}=null!;public Func<string,string> Translate{get;set;}=x=>x;
    public Func<int> SelectedPerson{get;set;}=()=>0;public Func<int> SelectedSite{get;set;}=()=>0;
    public Action<int>? SelectPerson{get;set;}public Action<int>? SelectSite{get;set;}public Action<int,int>? Assign{get;set;}
    private int dragged=-1;private Vector2 cursor;
    private readonly List<Rect2> labelRects=new();
    private Vector2 Camp=>new(Size.X*.5f,Size.Y*.52f);
    private Vector2 Location(HabitSite site)=>new((float)site.X*Size.X,(float)site.Y*Size.Y);
    private Vector2 Person(Settler p)
    {
        var site=Location(Read().Sites[p.Job]);var offset=new Vector2(Mathf.Cos(p.Id*2.4f)*43,Mathf.Sin(p.Id*2.4f)*43);
        var returnFrom=p.ReturnX.HasValue?new Vector2((float)p.ReturnX.Value*Size.X,(float)p.ReturnY!.Value*Size.Y):site;
        return p.Phase switch{"out"=>Camp.Lerp(site,Math.Min(1,p.Progress/30f))+offset,"home"=>returnFrom.Lerp(Camp,Math.Min(1,p.Progress/30f))+offset,"rest"=>Camp+offset*1.5f,_=>site+offset};
    }
    public Vector2 PersonScreen(int id)=>GlobalPosition+Person(Read().People.First(p=>p.Id==id));
    public Vector2 SiteScreen(int id)=>GlobalPosition+Location(Read().Sites[id]);
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseMotion motion)cursor=motion.Position;
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left} mouse)
        {
            if(mouse.Pressed)
            {
                var p=Read().People.OrderBy(p=>Person(p).DistanceTo(mouse.Position)).FirstOrDefault();
                if(p!=null&&Person(p).DistanceTo(mouse.Position)<32){dragged=p.Id;SelectPerson?.Invoke(p.Id);cursor=mouse.Position;}
                else {var site=Read().Sites.OrderBy(s=>Location(s).DistanceTo(mouse.Position)).First();if(Location(site).DistanceTo(mouse.Position)<90)SelectSite?.Invoke(site.Id);}
            }
            else if(dragged>=0)
            {
                var target=Read().Sites.OrderBy(s=>Location(s).DistanceTo(mouse.Position)).First();
                if(Location(target).DistanceTo(mouse.Position)<100){Assign?.Invoke(dragged,target.Id);SelectSite?.Invoke(target.Id);}dragged=-1;
            }
        }
    }
    private void Text(Vector2 at,string text,int size,Color color,bool separate=false,Vector2? anchor=null)
    {
        size=Math.Max(18,size);var font=GetThemeDefaultFont();float width=font.GetStringSize(text,HorizontalAlignment.Left,-1,size).X;
        var box=new Rect2(at.X-width*.5f-7,at.Y-size-4,width+14,size+12);
        if(separate&&labelRects.Any(r=>r.Intersects(box)))
        {
            var original=at;
            foreach(var offset in new[]{new Vector2(-80,0),new Vector2(80,0),new Vector2(0,-size-13),new Vector2(-80,-size-13),new Vector2(80,-size-13),new Vector2(0,-2*(size+13))})
            {
                var candidate=new Rect2(original.X+offset.X-width*.5f-7,original.Y+offset.Y-size-4,width+14,size+12);
                if(candidate.Position.X<4||candidate.End.X>Size.X-4||candidate.Position.Y<4||labelRects.Any(r=>r.Intersects(candidate)))continue;
                at=original+offset;box=candidate;break;
            }
            if(at!=original)DrawLine(anchor??original,box.GetCenter(),new Color(.95f,.93f,.8f,.75f),1.5f,true);
        }
        labelRects.Add(box);
        DrawStyleBox(new StyleBoxFlat{BgColor=new Color(.055f,.09f,.075f,.94f),CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5},box);
        var baseline=at-new Vector2(width*.5f,0);DrawStringOutline(font,baseline,text,HorizontalAlignment.Left,-1,size,3,new Color("101812"));
        DrawString(font,baseline,text,HorizontalAlignment.Left,-1,size,color);
    }
    public override void _Draw()
    {
        var s=Read();if(s==null)return;
        labelRects.Clear();
        DrawStyleBox(new StyleBoxFlat{BgColor=new Color("34473b"),CornerRadiusTopLeft=24,CornerRadiusTopRight=24,CornerRadiusBottomLeft=24,CornerRadiusBottomRight=24},new Rect2(Vector2.Zero,Size));
        for(int i=0;i<160;i++)
        {
            var p=new Vector2((float)((Math.Sin(i*39.7)*.5+.5)*(Size.X-30)+15),(float)((Math.Cos(i*19.1)*.5+.5)*(Size.Y-30)+15));
            DrawLine(p,p+new Vector2(3,-6),new Color(.4f,.55f,.36f,.25f),1);
        }
        DrawCircle(Location(s.Sites[2])+new Vector2(28,4),80,new Color("3d6470"));DrawCircle(Location(s.Sites[2])+new Vector2(30,0),59,new Color("547f87"));
        foreach(var site in s.Sites)
        {
            var p=Location(site);DrawLine(Camp,p,new Color("817960"),10,true);DrawLine(Camp,p,new Color("aaa07b"),2,true);
            if(site.EchoTrips>0)for(int i=0;i<18;i++){var q=Camp.Lerp(p,i/18f);DrawCircle(q,3,new Color("a2dce0"));}
            DrawCircle(p,63,new Color(site.Id==3?"786d51":"50684b"));
            if(SelectedSite()==site.Id)DrawArc(p,71,0,Mathf.Tau,60,new Color("e2c887"),2);
            if(site.Id==0)for(int i=0;i<5;i++)
            {var tree=p+new Vector2(Mathf.Cos(i*1.3f)*43,Mathf.Sin(i*1.3f)*29-5);DrawLine(tree+new Vector2(0,18),tree,new Color("655239"),6);DrawCircle(tree,19,new Color("799255"));DrawCircle(tree+new Vector2(7,0),4,new Color("d1ad62"));}
            if(site.Id==1)for(int i=0;i<12;i++){var stalk=p+new Vector2((i-6)*7,16);DrawLine(stalk,stalk+new Vector2(i%2==0?6:-4,-32),new Color("bdc286"),3);}
            if(site.Id==2){DrawArc(p,24,0,Mathf.Tau,32,new Color("b9d5ca"),7);DrawCircle(p,18,new Color("3a667b"));}
            if(site.Id==3){DrawRect(new Rect2(p-new Vector2(30,23),new Vector2(60,46)),new Color("c2a779"),false,5);for(int i=0;i<7;i++)DrawLine(p+new Vector2(-23+i*8,-19),p+new Vector2(-23+i*8,19),new Color("dfd4b3"),2);}
            Text(p+new Vector2(0,-83),Translate(site.Name),22,new Color("f1e7cb"));
            var hint=site.Practice?"rehearsing":site.Bound?"heavy_fruit":site.Claimed?"familiar_hands":"";
            if(hint!="")Text(p+new Vector2(0,site.Id==3?93:112),Translate(hint),15,new Color("f1d698"));
            if(site.Id!=3)Text(p+new Vector2(0,74),Translate(site.Resource)+" "+site.Reserve,14,new Color("d9dec3"));
            if(site.Bound){DrawCircle(p+new Vector2(-15,43),9,new Color("cdb78b"));DrawCircle(p+new Vector2(15,43),9,new Color("cdb78b"));DrawLine(p+new Vector2(-15,43),p+new Vector2(15,43),new Color("f5dfaa"),3);}
            if(site.EchoTrips>0)
            {
                float t=site.EchoProgress/30f;var ghost=t<1?Camp.Lerp(p,t):p.Lerp(Camp,t-1);
                DrawCircle(ghost,12,new Color(.68f,.9f,.95f,.42f));DrawArc(ghost,18,0,Mathf.Tau,24,new Color(.7f,.95f,1,.3f),2);
                var owner=s.People.First(x=>x.Id==site.EchoOwner);Text(ghost+new Vector2(0,-23),Translate(owner.Name)+" ?",14,new Color("c7eef0"));
                if(site.EchoCargo>0)DrawRect(new Rect2(ghost+new Vector2(9,0),new Vector2(9,9)),new Color("c5e9cf"));
            }
        }
        DrawCircle(Camp,51,new Color("8e7c5a"));for(int i=0;i<9;i++)DrawCircle(Camp+new Vector2(Mathf.Cos(i*.7f)*25,Mathf.Sin(i*.7f)*16),5,new Color("b7b198"));
        DrawLine(Camp+new Vector2(-13,6),Camp+new Vector2(13,-6),new Color("544635"),7);DrawLine(Camp+new Vector2(-13,-6),Camp+new Vector2(13,6),new Color("544635"),7);
        DrawCircle(Camp+new Vector2(0,-6),9,new Color("e4a35f"));Text(Camp+new Vector2(0,76),Translate("hearth"),20,new Color("f3e2bf"));
        foreach(var p in s.People)
        {
            var at=Person(p);var color=Color.FromHsv((p.Id*.17f+.07f)%1,.35f,.85f);
            DrawCircle(at+new Vector2(2,10),17,new Color(0,0,0,.45f));DrawLine(at+new Vector2(-6,9),at+new Vector2(-7,21),new Color("161c18"),5);DrawLine(at+new Vector2(6,9),at+new Vector2(7,21),new Color("161c18"),5);
            DrawCircle(at,15,new Color("101c16"));DrawCircle(at,12,color);DrawCircle(at+new Vector2(0,-15),10,new Color("101c16"));DrawCircle(at+new Vector2(0,-15),8,new Color("ffe0b3"));
            if(p.Cargo>0){DrawRect(new Rect2(at+new Vector2(11,-3),new Vector2(16,16)),new Color("17251d"));DrawRect(new Rect2(at+new Vector2(13,-1),new Vector2(12,12)),new Color("ffda82"));}
            Text(at+new Vector2(0,-35),Translate(p.Name),19,new Color("ffffff"),true,at);
            if(p.Id==SelectedPerson()){DrawArc(at,29,0,Mathf.Tau,40,new Color("ffdf6e"),4);Text(at+new Vector2(0,49),Translate(p.Thought),19,new Color("ffe393"),true,at);}
            if(p.Thought is "too_heavy" or "wrong_hands" or "need_fiber")Text(at+new Vector2(32,-7),"!",26,new Color("ffb467"),true,at);
        }
        if(s.GuestWaiting){var p=new Vector2(Size.X*.5f,Size.Y*.91f);DrawCircle(p,11,new Color("c1b3db"));Text(p+new Vector2(0,-20),Translate("visitor"),16,new Color("ede0f8"));}
        if(dragged>=0){DrawLine(Person(s.People.First(p=>p.Id==dragged)),cursor,new Color("f1d48a"),2);DrawArc(cursor,28,0,Mathf.Tau,32,new Color("f1d48a"),2);}
        Text(new Vector2(Size.X*.5f,28),Translate("map_hint"),15,new Color("e3d8b5"));
    }
}
