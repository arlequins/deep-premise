using Godot;
using DeepPremise.Core.Garden;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO=System.IO;

namespace DeepPremise.Godot;

public partial class GardenMain : Control
{
    private GardenWorld world=null!;
    private GardenState state=null!;
    private PlaytestRecorder recorder=null!;
    private Dictionary<string,string> words=new();
    private bool korean=true,paused,smoke,smokeExtended;
    private int selected=-1,smokeStage,trayPage;
    private string placing="",root="",save="",notice="";
    private double elapsed,saveClock,clock,noticeTime;
    private Label description=null!;
    private Button light=null!,pause=null!,contextAction=null!,previousPage=null!,nextPage=null!;
    private GardenCanvas garden=null!;
    private readonly Dictionary<string,Button> items=new();
    private string T(string key)=>words.GetValueOrDefault(key,key);
    public override void _Ready()
    {
        korean=!OS.GetCmdlineUserArgs().Contains("--language=en");smokeExtended=OS.GetCmdlineUserArgs().Contains("--smoke-garden-extended");smoke=smokeExtended||OS.GetCmdlineUserArgs().Contains("--smoke-garden");
        root=ProjectSettings.GlobalizePath("res://artifacts");IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save=smoke?IO.Path.Combine(root,"live","garden-smoke.json"):IO.Path.Combine(OS.GetUserDataDir(),"type3-garden-v11.json");
        string oldSave=IO.Path.Combine(OS.GetUserDataDir(),"type3-garden-v10.json");
        bool fresh=OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if(fresh&&!smoke&&IO.File.Exists(save))IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        string source=IO.File.Exists(save)?save:oldSave;
        world=!smoke&&!fresh&&IO.File.Exists(source)?GardenWorld.LoadJson(IO.File.ReadAllText(source)):new GardenWorld();
        Theme=new Theme{DefaultFont=new FontVariation{BaseFont=GD.Load<Font>("res://game/assets/NotoSansKR.ttf"),VariationEmbolden=.7f},DefaultFontSize=20};
        Theme.SetColor("font_color","Label",new Color("e5e9db"));Theme.SetColor("font_color","Button",new Color("e5e9db"));
        if(smokeExtended)world=CreateShowcase();
        recorder=new PlaytestRecorder(IO.Path.Combine(root,"playtests"),world,"type3-0.2",korean?"ko":"en");
        state=world.Observe();Build();Refresh();Persist();
    }
    private Button Button(string text,Action action)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(110,48)};
        foreach(string mode in new[]{"normal","hover","pressed"})
            b.AddThemeStyleboxOverride(mode,new StyleBoxFlat{BgColor=new Color(mode=="normal"?"2c403a":"425c4f"),CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14,ContentMarginLeft=22,ContentMarginRight=22});
        b.Pressed+=()=>{action();Refresh();};return b;
    }
    private void Build()
    {
        words=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/garden."+(korean?"ko":"en")+".json"))!;
        foreach(var c in GetChildren()){RemoveChild(c);c.QueueFree();}items.Clear();
        var bg=new ColorRect{Color=new Color("182823"),MouseFilter=MouseFilterEnum.Ignore};bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        var margin=new MarginContainer();margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(margin);
        foreach(string e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,28);
        var col=new VBoxContainer();col.AddThemeConstantOverride("separation",14);margin.AddChild(col);
        var header=new HBoxContainer();header.AddThemeConstantOverride("separation",10);col.AddChild(header);
        var title=new Label{Text=T("title"),SizeFlagsHorizontal=SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",28);header.AddChild(title);
        light=Button("",()=>{world.ToggleLight();Record("light",new{world.Observe().Night});recorder.Capture("light");});header.AddChild(light);
        pause=Button("",()=>{paused=!paused;Record("pause",new{paused});});header.AddChild(pause);
        header.AddChild(Button(korean?"EN":"한국어",()=>{korean=!korean;Record("language",new{korean});Build();}));
        garden=new GardenCanvas{Read=()=>state,Translate=T,Selected=()=>selected,Placing=()=>placing,Choose=id=>{selected=id;placing="";Record("select",new{id});Refresh();},Move=(id,x,y)=>{world.Move(id,x,y);Record("move",new{id,x,y});recorder.Capture("move");Refresh();},Place=(x,y)=>{if(placing!=""){int id=world.Place(placing,x,y);Record("place",new{kind=placing,x,y,id});recorder.Capture("place");selected=id;placing="";Refresh();}},SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(600,400)};col.AddChild(garden);
        var detailRow=new HBoxContainer();detailRow.AddThemeConstantOverride("separation",18);col.AddChild(detailRow);
        description=new Label{HorizontalAlignment=HorizontalAlignment.Center,CustomMinimumSize=new Vector2(0,54),SizeFlagsHorizontal=SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart};detailRow.AddChild(description);
        contextAction=Button("",()=>{
            if(selected==-2){bool accepted=world.StartExpedition();Record("expedition",new{accepted});if(!accepted)Show(world.ExpeditionRequirement());}
            else{bool accepted=world.Store(selected);Record("store",new{selected,accepted});if(accepted)selected=-1;}
            recorder.Capture("context_action");Persist();
        });detailRow.AddChild(contextAction);
        var tray=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};tray.AddThemeConstantOverride("separation",14);col.AddChild(tray);
        previousPage=Button("‹",()=>{trayPage--;});tray.AddChild(previousPage);previousPage.CustomMinimumSize=new Vector2(50,48);
        foreach(string kind in GardenWorld.Kinds)
        {string k=kind;var b=Button(T(k),()=>{placing=placing==k?"":k;selected=-1;Record("choose_item",new{kind=k});});items[k]=b;tray.AddChild(b);}
        nextPage=Button("›",()=>{trayPage++;});tray.AddChild(nextPage);nextPage.CustomMinimumSize=new Vector2(50,48);
    }
    private void Record(string action,object detail)=>recorder.Record("action",new{Action=action,Detail=detail});
    // A visual fixture only; progression is validated separately through simulation tests.
    private static GardenWorld CreateShowcase()
    {
        var sample=new GardenWorld().Observe();sample.Night=true;sample.DiscoveredBloom=true;sample.DiscoveredGlow=true;sample.DiscoveredFollowing=true;sample.DiscoveredSeed=true;sample.Trips=3;
        sample.Objects=new List<GardenObject>{
            new(){Id=0,Kind="stone",X=.3,Y=.52},new(){Id=1,Kind="snail",X=.39,Y=.59,Glow=1,Hue="blue"},
            new(){Id=2,Kind="grass",X=.45,Y=.64},new(){Id=3,Kind="pond",X=.23,Y=.7},new(){Id=4,Kind="beetle",X=.3,Y=.68,Dew=1},
            new(){Id=5,Kind="seed",X=.52,Y=.38,Growth=100,Hue="amber",Glow=.8},new(){Id=6,Kind="seed",X=.65,Y=.55,Growth=100,Hue="blue",Glow=.8},
            new(){Id=7,Kind="moth",X=.57,Y=.43},new(){Id=8,Kind="moss",X=.22,Y=.5},new(){Id=9,Kind="egg",X=.21,Y=.49,Growth=80,Age=0,Generation=1},
            new(){Id=10,Kind="moonstone",X=.73,Y=.67},new(){Id=11,Kind="seed",X=.77,Y=.52,Growth=100,Hue="moon",Glow=.8}
        };
        foreach(string k in GardenWorld.Kinds)sample.Pouch[k]=k=="egg"?0:2;
        return GardenWorld.LoadJson(JsonSerializer.Serialize(sample));
    }
    private void Refresh()
    {
        var next=world.Observe();
        if(next.DiscoveredBloom&&!state.DiscoveredBloom)Show("found_bloom");
        else if(next.DiscoveredSeed&&!state.DiscoveredSeed)Show("found_seed");
        else if(next.DiscoveredFollowing&&!state.DiscoveredFollowing)Show("found_following");
        else if(next.DiscoveredGlow&&!state.DiscoveredGlow)Show("found_glow");
        var discovery=next.Discoveries.Except(state.Discoveries).LastOrDefault();
        if(discovery!=null)Show("found_"+discovery);
        if(next.Trips>state.Trips)trayPage=next.Trips==1?1:next.Trips==2?2:2;
        state=next;
        light.Text=T(state.Night?"light_on":"light_off");pause.Text=T(paused?"resume":"pause");
        var kinds=GardenWorld.Kinds.Where(k=>k is "snail" or "stone" or "grass" || state.Pouch[k]>0 || state.Objects.Any(o=>o.Kind==k)).ToArray();
        int pages=(kinds.Length+2)/3;trayPage=Math.Clamp(trayPage,0,pages-1);
        foreach(var (k,b) in items){b.Text=T(k)+"  ·  "+state.Pouch[k];b.Disabled=state.Pouch[k]==0||state.Objects.Count>=36;b.Visible=kinds.Skip(trayPage*3).Take(3).Contains(k);b.Modulate=placing==k?new Color("ffe8a5"):Colors.White;}
        previousPage.Visible=nextPage.Visible=pages>1;previousPage.Disabled=trayPage==0;nextPage.Disabled=trayPage==pages-1;
        var o=state.Objects.FirstOrDefault(o=>o.Id==selected);
        contextAction.Visible=selected==-2||o!=null;
        contextAction.Text=T(selected==-2?"trip_"+Math.Min(state.Trips,3):"store");contextAction.Disabled=selected==-2?world.ExpeditionRequirement()!="ready":o?.Away==true;
        string name=o==null?"":(o.Kind=="snail"&&o.Generation>0?T(o.Age<600?"young":"grown_name")+" ":"")+(o.Kind is "snail" or "seed"&&o.Glow>0?T("hue_"+o.Hue)+" ":"")+T(o.Kind=="seed"&&o.Growth>=100?"flower":o.Kind);
        description.Text=noticeTime>0?T(notice):placing!=""?string.Format(T("place_hint"),T(placing)):selected==-2?(state.ExpeditionTicks>0?string.Format(T("trip_progress"),(state.ExpeditionTicks+9)/10):T(world.ExpeditionRequirement())):o!=null?name+"  —  "+T(o.Behavior):T(NextHint());
        garden.QueueRedraw();
    }
    private string NextHint()
    {
        if(state.Discoveries.Contains("harmony"))return "harmony_hint";
        if(state.Trips>=3)return state.Discoveries.Contains("hatched")?"colors_hint":"breed_hint";
        if(state.Trips==2)return "pollination_hint";
        if(state.Trips==1)return "water_hint";
        return state.DiscoveredBloom?"expedition_hint":state.DiscoveredSeed?"seed_hint":state.DiscoveredFollowing?"cave_hint":state.DiscoveredGlow?"grass_hint":"first_hint";
    }
    private void Show(string key){notice=key;noticeTime=7;}
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is not InputEventKey{Pressed:true,Echo:false}key)return;
        if(key.Keycode==Key.Space){paused=!paused;Record("pause",new{paused});}
        if(key.Keycode==Key.Escape){placing="";selected=-1;}
        if(key.Keycode==Key.F8){Record("feedback",new{selected,placing});recorder.Capture("feedback");Show("marked");Persist();}Refresh();
    }
    public override void _Process(double delta)
    {
        elapsed+=delta;clock+=delta;saveClock+=delta;noticeTime-=delta;
        if(elapsed>=.1){int ticks=Math.Min(10,(int)(elapsed/.1));elapsed-=ticks*.1;if(!paused&&!garden.Dragging)world.Run(ticks);Refresh();}
        garden.Clock+=(float)delta;garden.QueueRedraw();
        if(saveClock>=3){saveClock=0;Persist();GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","type3-screen.png"));}
        if(smokeExtended)
        {
            if(clock>.5&&smokeStage==0){NativeClick(garden.Point(.87,.25));smokeStage++;}
            if(clock>.9&&smokeStage==1){NativeClick(contextAction.GetGlobalRect().GetCenter());smokeStage++;}
            if(clock>1.2&&smokeStage==2){NativeClick(nextPage.GetGlobalRect().GetCenter());smokeStage++;}
            if(clock>1.5&&smokeStage==3){NativeClick(items["beetle"].GetGlobalRect().GetCenter());smokeStage++;}
            if(clock>1.8&&smokeStage==4){NativeClick(garden.Point(.32,.28));smokeStage++;}
        }
        if(smokeExtended&&clock>3)
        {
            bool ok=state.ExpeditionTicks>0&&state.Pouch["beetle"]==1&&items.Values.Count(b=>b.Visible)<=3;
            GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"garden-expanded-ko.png":"garden-expanded-en.png"));GD.Print("GARDEN EXPANDED INPUT "+(ok?"PASS":"FAIL"));GetTree().Quit(ok?0:1);
        }
        if(smoke&&!smokeExtended)
        {
            if(clock>.5&&smokeStage==0){NativeClick(light.GetGlobalRect().GetCenter());smokeStage++;}
            if(clock>.8&&smokeStage==1){NativeMouse(garden.Point(.31,.56),true);smokeStage++;}
            if(clock>1.1&&smokeStage==2){NativeMouse(garden.Point(.47,.55),false);smokeStage++;world.Run(100);Refresh();}
            if(clock>1.5&&smokeStage==3){NativeClick(items["grass"].GetGlobalRect().GetCenter());smokeStage++;}
            if(clock>1.8&&smokeStage==4){NativeClick(garden.Point(.5,.57));smokeStage++;world.Run(35);Refresh();}
            if(clock>3){bool ok=state.Night&&state.DiscoveredGlow&&state.DiscoveredFollowing&&state.Pouch["grass"]==2;GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"garden-ko.png":"garden-en.png"));GD.Print("GARDEN NATIVE INPUT "+(ok?"PASS":"FAIL"));GetTree().Quit(ok?0:1);}
        }
    }
    private static void NativeMouse(Vector2 point,bool down)=>Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=down});
    private static void NativeClick(Vector2 point){NativeMouse(point,true);NativeMouse(point,false);}
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);IO.File.WriteAllText(save+".tmp",world.SaveJson());IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live",smoke?"type3-smoke-context.json":"type3-context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="type3-0.2",world.Tick,Paused=paused,Session=recorder.DirectoryPath,State=world.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree(){Persist();recorder.Dispose();}
}

public partial class GardenCanvas : Control
{
    public Func<GardenState> Read=null!;
    public Func<string,string> Translate=null!;
    public Func<int> Selected=()=>-1;
    public Func<string> Placing=()=>"";
    public Action<int> Choose=null!;
    public Action<int,double,double> Move=null!;
    public Action<double,double> Place=null!;
    public float Clock;
    private int dragging=-1;
    private Vector2 cursor,pressedAt;
    public bool Dragging=>dragging>=0;
    private Rect2 Area=>new(new Vector2(60,45),Size-new Vector2(120,90));
    public Vector2 Point(double x,double y)=>GlobalPosition+At(x,y);
    private Vector2 At(double x,double y)=>Area.Position+new Vector2((float)x,(float)y)*Area.Size;
    private Vector2 ObjectPosition(GardenObject o)=>dragging==o.Id?cursor:At(o.X,o.Y);
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseMotion m)cursor=m.Position;
        if(input is not InputEventMouseButton{ButtonIndex:MouseButton.Left}b)return;
        cursor=b.Position;
        if(b.Pressed)
        {
            if(!Area.HasPoint(cursor))return;
            if(Placing()!=""){var uv=(cursor-Area.Position)/Area.Size;Place(uv.X,uv.Y);return;}
            var near=Read().Objects.Where(o=>!o.Away).OrderBy(o=>At(o.X,o.Y).DistanceTo(cursor)).FirstOrDefault();
            if(At(.87,.25).DistanceTo(cursor)<36&&Read().DiscoveredBloom&&(near==null||At(near.X,near.Y).DistanceTo(cursor)>32)){Choose(-2);return;}
            if(near!=null&&At(near.X,near.Y).DistanceTo(cursor)<48){dragging=near.Id;pressedAt=cursor;Choose(near.Id);}else Choose(-1);
        }
        else if(dragging>=0)
        {if(cursor.DistanceTo(pressedAt)>5){var uv=(cursor-Area.Position)/Area.Size;Move(dragging,uv.X,uv.Y);}dragging=-1;}
    }
    private void Glow(Vector2 p,float radius,Color color)
    {for(int i=32;i>=1;i--)DrawCircle(p,radius*i/32,color with{A=.002f+(32-i)*.0004f});}
    public override void _Draw()
    {
        var s=Read();
        DrawStyleBox(new StyleBoxFlat{BgColor=new Color(s.Night?"233f38":"526e55"),BorderColor=new Color("6a8370"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=5,CornerRadiusTopLeft=70,CornerRadiusTopRight=70,CornerRadiusBottomLeft=70,CornerRadiusBottomRight=70},new Rect2(new Vector2(4,4),Size-new Vector2(8,8)));
        Glow(At(.28,.26),250,new Color(s.Night?"578f87":"f6df9b"));
        if(s.Night&&s.Discoveries.Contains("harmony"))
            for(int k=0;k<7;k++)Glow(At(.12+k*.12,.2+Math.Sin(Clock*.2+k)*.08),95,new Color(k%2==0?"7dc7ab":"9c9dd1"));
        for(int i=0;i<28;i++)
        {
            float x=.06f+(i*37%89)/100f,y=.09f+(i*19%79)/100f;
            DrawCircle(At(x,y),i%3+2,new Color(s.Night?"34534a":"66835f"));
        }
        for(int i=0;i<10;i++)
        {
            var p=At(.035+i*.101,.88+(i%2)*.015);
            DrawArc(p,13+i%3*5,Mathf.Pi,Mathf.Tau,12,new Color(s.Night?"456750":"789770"),4,true);
        }
        var cave=At(.87,.25);
        DrawCircle(cave+new Vector2(0,8),39,new Color(s.Night?"3f5348":"768571"));DrawCircle(cave,28,new Color("172e2a"));
        if(!s.DiscoveredSeed){Glow(cave,32,new Color("c5d6b8"));DrawCircle(cave+new Vector2(7,6),3,new Color("ccdeb7"));}
        if(s.DiscoveredBloom)
        {
            DrawArc(cave,35,-Mathf.Pi/2,-Mathf.Pi/2+Mathf.Tau*(s.ExpeditionTicks>0?1-(float)s.ExpeditionTicks/s.ExpeditionDuration:1),48,new Color(s.ExpeditionTicks>0?"a8c9c7":"d7c995"),3,true);
            if(s.ExpeditionTicks>0)DrawCircle(cave+new Vector2(Mathf.Cos(Clock),Mathf.Sin(Clock))*18,4,new Color("ead58b"));
        }
        foreach(var o in s.Objects.Where(o=>!o.Away).OrderBy(o=>o.Kind is "pond" or "moss"?0:1).ThenBy(o=>o.Y))
        {
            var p=ObjectPosition(o);float pulse=Mathf.Sin(Clock*2+o.Id);
            DrawSetTransform(p+new Vector2(0,17),0,new Vector2(1,.35f));DrawCircle(Vector2.Zero,35,new Color(0,0,0,.15f));DrawSetTransform(Vector2.Zero);
            if(o.Id==Selected())DrawArc(p,43,0,Mathf.Tau,48,new Color("d6ddb5"),2,true);
            if(o.Kind=="pond")
            {
                DrawSetTransform(p,0,new Vector2(1,.6f));DrawCircle(Vector2.Zero,56,new Color("72918b"));DrawCircle(Vector2.Zero,48,new Color("457f8b"));
                DrawArc(new Vector2(-7,-5),27,0,Mathf.Tau,36,new Color("8fc3c6"),2,true);DrawArc(new Vector2(8,7),15,0,Mathf.Tau,30,new Color("699da9"),1,true);DrawSetTransform(Vector2.Zero);
            }
            if(o.Kind=="moss")
            {for(int k=0;k<5;k++)DrawCircle(p+new Vector2(Mathf.Cos(k*1.2f)*24,Mathf.Sin(k*1.2f)*12),22,new Color(k%2==0?"658d63":"78996b"));}
            if(o.Kind=="moonstone")
            {
                if(s.Night)Glow(p,130,new Color("ada4e9"));
                DrawColoredPolygon(new[]{p+new Vector2(-22,21),p+new Vector2(-12,-24),p+new Vector2(6,-37),p+new Vector2(25,13),p+new Vector2(14,24)},new Color(s.Night?"b6b5d4":"7e8996"));
                DrawLine(p+new Vector2(6,-31),p+new Vector2(2,20),new Color("e2ddef"),3,true);
            }
            if(o.Kind=="beetle")
            {
                for(int k=0;k<3;k++){DrawLine(p+new Vector2(-12,-7+k*8),p+new Vector2(-23,-13+k*12),new Color("7eacb0"),3);DrawLine(p+new Vector2(12,-7+k*8),p+new Vector2(23,-13+k*12),new Color("7eacb0"),3);}
                DrawCircle(p,18,new Color("4a9295"));DrawLine(p+new Vector2(0,-14),p+new Vector2(0,14),new Color("285459"),2);
                DrawCircle(p+new Vector2(0,-19),8,new Color("68999b"));
                if(o.Dew>0){DrawCircle(p,9,new Color("b4e2ed"));DrawCircle(p+new Vector2(-3,-3),3,Colors.White);}
            }
            if(o.Kind=="moth")
            {
                float spread=s.Night?10:18+pulse*5;
                DrawSetTransform(p,0,new Vector2(1,.8f));DrawCircle(new Vector2(-spread,-3),17,new Color("e2cbcd"));DrawCircle(new Vector2(spread,-3),17,new Color("e2cbcd"));DrawCircle(new Vector2(-spread,13),12,new Color("b9a4bd"));DrawCircle(new Vector2(spread,13),12,new Color("b9a4bd"));DrawSetTransform(Vector2.Zero);
                DrawLine(p+new Vector2(0,-14),p+new Vector2(0,19),new Color("ecd7ac"),6,true);
            }
            if(o.Kind=="egg")
            {
                for(int k=0;k<3;k++){var ep=p+new Vector2((k-1)*12,k%2*6);DrawCircle(ep,9,new Color("e4dfb9"));DrawCircle(ep+new Vector2(-2,-3),3,new Color("fff3d4"));}
            }
            if(o.Kind=="stone")
            {
                if(s.Night)Glow(p,100,new Color("ffc58b"));
                DrawColoredPolygon(new[]{p+new Vector2(-31,11),p+new Vector2(-26,-14),p+new Vector2(-6,-27),p+new Vector2(22,-20),p+new Vector2(35,5),p+new Vector2(20,23),p+new Vector2(-16,24)},new Color(s.Night?"b68d67":"96a392"));
                DrawLine(p+new Vector2(-17,-8),p+new Vector2(7,-17),new Color(s.Night?"edc798":"bdc6ae"),4,true);
                if(s.Night)DrawCircle(p+new Vector2(8,7),3,new Color("ffdb9e"));
            }
            if(o.Kind=="snail")
            {
                if(o.Age<600){DrawSetTransform(p,0,new Vector2(.62f,.62f));p=Vector2.Zero;}
                if(o.Glow>=1)Glow(p,115,new Color("f3df81"));
                DrawLine(p+new Vector2(-25,15),p+new Vector2(30,15),new Color("bfcd9c"),15,true);
                DrawCircle(p+new Vector2(28,9),10,new Color("cbd9a9"));
                DrawLine(p+new Vector2(27,4),p+new Vector2(24,-9),new Color("cbd9a9"),3,true);DrawLine(p+new Vector2(33,5),p+new Vector2(39,-5),new Color("cbd9a9"),3,true);
                DrawCircle(p+new Vector2(24,-9),3,new Color("182b26"));DrawCircle(p+new Vector2(39,-5),3,new Color("182b26"));
                DrawCircle(p+new Vector2(-4,-4),25,new Color(o.Glow>=1?HueColor(o.Hue):"c68e70"));
                var spiral=new List<Vector2>();for(int n=0;n<60;n++){float a=n*.18f,r=2+n*.28f;spiral.Add(p+new Vector2(-4,-4)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r);}DrawPolyline(spiral.ToArray(),new Color(o.Glow>=1?"b09255":"915e50"),2,true);
                if(o.Glow>=1)DrawCircle(p+new Vector2(-10,-18),4,new Color("fff4c5"));
            }
            if(o.Kind=="grass")
            {
                DrawCircle(p+new Vector2(0,14),18,new Color("718b56"));
                for(int i=0;i<7;i++)
                {float dx=(i-3)*9;var end=p+new Vector2(dx+pulse*3,-18-Mathf.Sin(i*1.3f)*13);DrawLine(p+new Vector2((i-3)*3,15),end,new Color(i%2==0?"b7cb8d":"88ae7a"),5,true);}
                if(o.Following>=0){DrawCircle(p+new Vector2(-6,9),2,new Color("354b37"));DrawCircle(p+new Vector2(6,9),2,new Color("354b37"));}
            }
            if(o.Kind=="seed")
            {
                if(o.Growth>=100){Glow(p,100,new Color(HueColor(o.Hue)));for(int k=0;k<5;k++)DrawCircle(p+new Vector2(Mathf.Cos(k*Mathf.Tau/5),Mathf.Sin(k*Mathf.Tau/5))*14,11,new Color(HueColor(o.Hue)));DrawCircle(p,8,new Color("f7df9c"));}
                else{DrawCircle(p,10,new Color("b8b18a"));if(o.Growth>0)DrawLine(p,p+new Vector2(3,-o.Growth*.2f),new Color("abc992"),4,true);}
            }
            DrawSetTransform(Vector2.Zero);
        }
        if(Placing()!=""&&Area.HasPoint(cursor)){DrawArc(cursor,36,0,Mathf.Tau,40,new Color("e7e3b7"),2,true);DrawLine(cursor-new Vector2(8,0),cursor+new Vector2(8,0),new Color("e7e3b7"),2);DrawLine(cursor-new Vector2(0,8),cursor+new Vector2(0,8),new Color("e7e3b7"),2);}
    }
    private static string HueColor(string hue)=>hue switch{"blue"=>"99cedb","moon"=>"c8b9e5","pearl"=>"e9c2cd",_=>"ead58b"};
}
