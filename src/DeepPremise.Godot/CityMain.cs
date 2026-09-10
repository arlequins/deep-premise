using Godot;
using DeepPremise.Core.City;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO = System.IO;

namespace DeepPremise.Godot;

public partial class CityMain : Control
{
    private CityWorld world = null!;
    private CityState state = null!;
    private PlaytestRecorder recorder = null!;
    private Dictionary<string,string> en = new(), ko = new();
    private bool korean = true, paused, smoke;
    private int speed = 1, selected = -1, selectedPerson = -1, smokeStage;
    private string tool = "inspect", message = "", root = "", save = "";
    private double accumulator, saveClock, smokeClock;
    private Label stats = null!, detail = null!, journal = null!, hint = null!, needs = null!;
    private Button pauseButton = null!;
    private CityCanvas canvas = null!;
    private readonly Dictionary<string,Button> buttons = new();
    private string T(string key) => (korean ? ko : en).GetValueOrDefault(key, key);
    public override void _Ready()
    {
        en = JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/city.en.json"))!;
        ko = JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/city.ko.json"))!;
        korean = !OS.GetCmdlineUserArgs().Contains("--language=en");
        smoke = OS.GetCmdlineUserArgs().Contains("--smoke-city");
        root = ProjectSettings.GlobalizePath("res://artifacts");
        IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save = smoke ? IO.Path.Combine(root,"live","city-smoke.json") : IO.Path.Combine(OS.GetUserDataDir(),"type2-city-v9.json");
        bool fresh = OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if (fresh && !smoke && IO.File.Exists(save)) IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        world = !smoke && !fresh && IO.File.Exists(save) ? CityWorld.LoadJson(IO.File.ReadAllText(save)) : new CityWorld();
        recorder = new PlaytestRecorder(IO.Path.Combine(root,"playtests"),world,"type2-0.1",korean ? "ko" : "en");
        Theme = new Theme { DefaultFont = new FontVariation { BaseFont = GD.Load<Font>("res://game/assets/NotoSansKR.ttf"), VariationEmbolden = .4f }, DefaultFontSize = 18 };
        Theme.SetColor("font_color","Label",new Color("e9f0e9"));
        Theme.SetColor("font_color","Button",Colors.White);
        state = world.Observe(); BuildUI(); Refresh(); Persist();
        if (smoke) { world.Run(960); Refresh(); }
    }
    private Button MakeButton(string text, Action action)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0,42) };
        foreach (string mode in new[]{"normal","hover","pressed"})
            b.AddThemeStyleboxOverride(mode,new StyleBoxFlat { BgColor = new Color(mode == "normal" ? "344940" : "52695a"), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginLeft = 12, ContentMarginRight = 12 });
        b.Pressed += () => { action(); Refresh(); }; return b;
    }
    private Label Label(string text, int size = 18)
    { var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; l.AddThemeFontSizeOverride("font_size",size); return l; }
    private void BuildUI()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); } buttons.Clear();
        var bg = new ColorRect { Color = new Color("1d302b"), MouseFilter = MouseFilterEnum.Ignore }; bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(bg);
        var margins = new MarginContainer(); margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(margins);
        foreach (string e in new[]{"left","right","top","bottom"}) margins.AddThemeConstantOverride("margin_"+e,20);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation",12); margins.AddChild(layout);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation",12); layout.AddChild(top);
        var title = Label(T("title"),28); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(title);
        pauseButton = MakeButton(T("pause"),()=> { paused = !paused; Record("pause",new{paused}); }); top.AddChild(pauseButton);
        top.AddChild(MakeButton("1× / 3× / 8×",()=> { speed = speed == 1 ? 3 : speed == 3 ? 8 : 1; Record("speed",new{speed}); }));
        top.AddChild(MakeButton("한국어 / EN",()=> { korean = !korean; Record("language",new{korean}); BuildUI(); }));
        stats = Label("",23); layout.AddChild(stats);
        needs = Label("",17); needs.Modulate = new Color("f4dca8"); layout.AddChild(needs);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation",20); layout.AddChild(body);
        canvas = new CityCanvas { Read = ()=>state, Translate = T, Selected = ()=>selected, SelectedPerson = ()=>selectedPerson, Tool = ()=>tool, Click = Click, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(650,480) }; body.AddChild(canvas);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(340,0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(318,0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; side.AddThemeConstantOverride("separation",10); scroll.AddChild(side);
        side.AddChild(Label(T("tools"),22));
        var tools = new GridContainer { Columns = 2 }; side.AddChild(tools);
        foreach (string key in new[]{"inspect","road","home","workshop","market","park","erase"})
        {
            string k = key; var button = MakeButton(T(k)+(CityWorld.Prices.TryGetValue(k,out int cost) && cost > 0 ? "  "+cost : ""),()=> { tool = k; message = ""; });
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; buttons[k] = button; tools.AddChild(button);
        }
        side.AddChild(Label(T("gentle_hint"),16)); side.AddChild(new HSeparator());
        detail = Label(""); side.AddChild(detail);
        side.AddChild(new HSeparator()); side.AddChild(Label(T("journal"),22));
        journal = Label("",17); side.AddChild(journal);
        hint = Label("",17); layout.AddChild(hint);
    }
    private void Record(string action, object data) => recorder.Record("action",new { Action = action, Detail = data });
    private void Click(int tile, int person)
    {
        selected = tile; selectedPerson = tool == "inspect" ? person : -1;
        if (tool != "inspect")
        {
            message = world.Build(tile,tool); Record("build",new{tile,tool,result=message}); recorder.Capture("build"); Persist();
        }
        else Record("inspect",new{tile,person});
        Refresh();
    }
    private string CitizenName(int id) => T("name"+(id % 12)) + (id >= 12 ? " "+(id / 12 + 1) : "");
    private string Place(int index) => index < 0 ? T("none") : T(state.Tiles[index].Kind)+" ("+(index%CityWorld.Width+1)+", "+(index/CityWorld.Width+1)+")";
    private void Refresh()
    {
        state = world.Observe(); int time = (int)(state.Tick % 240);
        stats.Text = string.Format(T("stats"),state.Day,state.Funds,state.People.Count,(int)state.People.Average(p=>p.Happiness),T(time<25||time>=205?"night":time<130?"morning":"afternoon"),speed);
        pauseButton.Text = T(paused ? "resume" : "pause");
        int emptyBeds = state.Tiles.Where(t=>t.Kind=="home"&&t.Connected).Sum(t=>t.Level==1?4:6)-state.People.Count;
        int jobless = state.People.Count(p=>p.Work<0);
        needs.Text = jobless>0 ? string.Format(T("need_jobs"),jobless) : emptyBeds<2 ? T("need_homes") : T("steady");
        foreach (var (key,b) in buttons) b.Modulate = key == tool ? new Color("ffe1a0") : Colors.White;
        if (selectedPerson >= 0)
        {
            var p = state.People.First(x=>x.Id==selectedPerson);
            detail.Text = CitizenName(p.Id)+"\n"+T(p.Activity)+(p.Route.Count>0?" · "+T("walking"):"")+"\n\n"+T("home")+": "+Place(p.Home)+"\n"+T("work")+": "+Place(p.Work)+"\n"+T("likes")+": "+T(p.Preference)+"\n"+T("favorite")+": "+Place(p.Favorite)+"\n"+string.Format(T("happiness"),p.Happiness);
        }
        else if (selected >= 0)
        {
            var t = state.Tiles[selected];
            detail.Text = Place(selected)+"\n"+T(t.Kind+"_desc")+(t.Kind is "empty" or "river" or "road"?"":"\n\n"+T(t.Connected?"connected":"disconnected"))+"\n";
            if (t.Kind == "home") detail.Text += string.Format(T("residents"),state.People.Count(p=>p.Home==selected),t.Level==1?4:6);
            if (t.Kind is "market" or "park") detail.Text += string.Format(T("visits"),t.Visits)+"\n"+T(t.Level==2?(t.Kind=="park"?"festival_short":"night_market_short"):"identity_hint");
            if (t.Kind == "workshop") detail.Text += string.Format(T("workers"),state.People.Count(p=>p.Work==selected));
        }
        else detail.Text = T("start");
        journal.Text = string.Join("\n\n",state.Journal.TakeLast(4).Reverse().Select(e=>string.Format(T("day"),e.Day)+" · "+(e.Key=="budget"?string.Format(T("budget"),e.Value):T(e.Key))));
        hint.Text = message != "" && message != "ok" ? T(message) : T(tool=="inspect"?"controls":"build_controls");
        canvas.QueueRedraw();
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed:true,Echo:false } key) return;
        if (key.Keycode == Key.Space) { paused = !paused; Record("pause",new{paused}); }
        if (key.Keycode == Key.Escape) { tool = "inspect"; message = ""; }
        if (key.Keycode == Key.F8) { Record("feedback",new{selected,selectedPerson,tool}); recorder.Capture("feedback"); message = "marked"; Persist(); }
        Refresh();
    }
    public override void _Process(double delta)
    {
        accumulator += delta; saveClock += delta; smokeClock += delta;
        if (accumulator >= .22) { int ticks = Math.Min(12,(int)(accumulator/.22)); accumulator -= ticks*.22; if(!paused) world.Run(ticks*speed); Refresh(); }
        canvas.Clock += (float)delta; canvas.QueueRedraw();
        if (smoke && smokeClock > .8 && smokeStage == 0) { NativeClick(buttons["road"].GetGlobalRect().GetCenter()); smokeStage++; }
        if (smoke && smokeClock > 1.2 && smokeStage == 1) { NativeClick(canvas.PlotScreen(4*CityWorld.Width+3)); smokeStage++; }
        if (smoke && smokeClock > 1.6 && smokeStage == 2) { NativeClick(buttons["inspect"].GetGlobalRect().GetCenter()); smokeStage++; }
        if (saveClock >= 3) { saveClock = 0; Persist(); GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","type2-screen.png")); }
        if (smoke && smokeClock > 3)
        {
            GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"city-ko.png":"city-en.png"));
            bool ok = state.Tiles[4*CityWorld.Width+3].Kind == "road";
            GD.Print("CITY NATIVE INPUT "+(ok?"PASS":"FAIL")+": population="+state.People.Count); GetTree().Quit(ok?0:1);
        }
    }
    private void NativeClick(Vector2 position)
    {
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=true });
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=false });
    }
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!); IO.File.WriteAllText(save+".tmp",world.SaveJson()); IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live",smoke?"type2-smoke-context.json":"type2-context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="type2-0.1",world.Tick,Paused=paused,Session=recorder.DirectoryPath,State=world.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree() { Persist(); recorder.Dispose(); }
}

public partial class CityCanvas : Control
{
    public Func<CityState> Read = null!;
    public Func<string,string> Translate = null!;
    public Func<int> Selected = ()=>-1, SelectedPerson = ()=>-1;
    public Func<string> Tool = ()=>"inspect";
    public Action<int,int> Click = null!;
    public float Clock;
    private int hover = -1;
    private readonly Dictionary<int,Vector2> positions = new();
    private float Cell => Math.Min((Size.X-32)/CityWorld.Width,(Size.Y-100)/CityWorld.Height);
    private Vector2 Origin => new((Size.X-Cell*CityWorld.Width)/2,48+(Size.Y-100-Cell*CityWorld.Height)/2);
    private Vector2 Center(int i) => Origin+new Vector2(i%CityWorld.Width+.5f,i/CityWorld.Width+.5f)*Cell;
    public Vector2 PlotScreen(int i) => GlobalPosition+Center(i);
    private Vector2 Target(Citizen p) => Center(p.At)+new Vector2(Mathf.Sin(p.Id*4.1f),Mathf.Cos(p.Id*2.3f))*Cell*.2f;
    private Vector2 Person(Citizen p) => positions.GetValueOrDefault(p.Id,Target(p));
    public override void _Process(double delta)
    {
        foreach(var p in Read().People) positions[p.Id] = Person(p).MoveToward(Target(p),(float)delta*Cell*3);
    }
    public override void _GuiInput(InputEvent e)
    {
        Vector2 pos = e is InputEventMouse m ? m.Position : Vector2.Zero;
        var local = (pos-Origin)/Cell; int x = (int)Math.Floor(local.X), y = (int)Math.Floor(local.Y);
        hover = x>=0&&x<CityWorld.Width&&y>=0&&y<CityWorld.Height ? y*CityWorld.Width+x : -1;
        if(e is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left } && hover>=0)
        {
            var p = Read().People.OrderBy(p=>Person(p).DistanceTo(pos)).FirstOrDefault();
            Click(hover,p!=null&&Person(p).DistanceTo(pos)<12?p.Id:-1);
        }
    }
    private void Text(Vector2 at,string text,int size,Color color) => DrawString(GetThemeDefaultFont(),at,text,HorizontalAlignment.Left,-1,size,color);
    private void Box(Rect2 rect,string color) => DrawRect(rect,new Color(color));
    public override void _Draw()
    {
        var s = Read(); float c = Cell;
        DrawStyleBox(new StyleBoxFlat { BgColor = new Color("c9dbc0"),CornerRadiusTopLeft=16,CornerRadiusTopRight=16,CornerRadiusBottomLeft=16,CornerRadiusBottomRight=16 },new Rect2(Vector2.Zero,Size));
        Text(new Vector2(20,30),Translate("map_title"),20,new Color("2f4b40"));
        for(int i=0;i<s.Tiles.Count;i++)
        {
            var t=s.Tiles[i]; var at=Origin+new Vector2(i%CityWorld.Width,i/CityWorld.Width)*c; var r=new Rect2(at,new Vector2(c,c));
            Box(r,t.Kind=="river"?"81b8c1":(i+i/CityWorld.Width)%2==0?"c0d3b1":"bad0aa");
            if(t.Kind=="empty")
            { if((i*17)%11==0){DrawCircle(at+new Vector2(c*.6f,c*.65f),c*.13f,new Color("9eb98e"));} }
            if(t.Kind=="river")
            { DrawLine(at+new Vector2(5,c*.5f),at+new Vector2(c-8,c*.5f+Mathf.Sin(Clock+i)*2),new Color("aad4d6"),2); }
            if(t.Kind=="road")
            {
                Box(r.Grow(-1),"e7dbc0");
                DrawLine(at+new Vector2(0,c*.5f),at+new Vector2(c,c*.5f),new Color("c8bca4"),1);
            }
            if(t.Kind is "home" or "workshop" or "market")
            {
                var building=new Rect2(at+new Vector2(c*.15f,c*.22f),new Vector2(c*.7f,c*.65f));
                Box(new Rect2(building.Position+new Vector2(3,4),building.Size),"90a187");
                Box(building,t.Kind=="home"?"f5e7cb":t.Kind=="workshop"?"b0bac0":"f1d5a1");
                if(t.Kind=="home")
                {
                    DrawColoredPolygon(new[]{at+new Vector2(c*.1f,c*.32f),at+new Vector2(c*.5f,c*.07f),at+new Vector2(c*.9f,c*.32f)},new Color(t.Level==2?"426e71":"bf7861"));
                    Box(new Rect2(at+new Vector2(c*.43f,c*.57f),new Vector2(c*.16f,c*.3f)),"836e5d");
                    Box(new Rect2(at+new Vector2(c*.23f,c*.42f),new Vector2(c*.14f,c*.15f)),"89b5b6");
                    if(t.Level==2) Box(new Rect2(at+new Vector2(c*.65f,c*.42f),new Vector2(c*.14f,c*.15f)),"89b5b6");
                }
                if(t.Kind=="workshop")
                {
                    Box(new Rect2(at+new Vector2(c*.13f,c*.2f),new Vector2(c*.74f,c*.18f)),"607a83");
                    Box(new Rect2(at+new Vector2(c*.67f,c*.06f),new Vector2(c*.12f,c*.25f)),"647778");
                    for(int w=0;w<3;w++) Box(new Rect2(at+new Vector2(c*(.24f+w*.19f),c*.5f),new Vector2(c*.12f,c*.2f)),"e0c896");
                    DrawCircle(at+new Vector2(c*.73f,c*(.03f-.05f*Mathf.Sin(Clock))),c*.07f,new Color("e8e4d3"));
                }
                if(t.Kind=="market")
                {
                    for(int w=0;w<5;w++) Box(new Rect2(at+new Vector2(c*(.1f+w*.16f),c*.2f),new Vector2(c*.16f,c*.22f)),w%2==0?"d57c68":"faf0d8");
                    Box(new Rect2(at+new Vector2(c*.23f,c*.61f),new Vector2(c*.54f,c*.12f)),"a58455");
                }
                if(!t.Connected) { DrawCircle(at+new Vector2(c*.85f,c*.1f),8,new Color("b6473c")); Text(at+new Vector2(c*.85f-3,c*.1f+5),"!",15,Colors.White); }
            }
            if(t.Kind=="park")
            {
                Box(r.Grow(-3),"93b887");
                DrawCircle(at+new Vector2(c*.35f,c*.4f),c*.23f,new Color("517f64"));
                DrawCircle(at+new Vector2(c*.65f,c*.6f),c*.2f,new Color("6c9870"));
                Box(new Rect2(at+new Vector2(c*.19f,c*.74f),new Vector2(c*.35f,4)),"ebd8ad");
            }
            if(t.Level==2 && t.Kind is "park" or "market")
            {
                DrawLine(at+new Vector2(2,5),at+new Vector2(c-2,5),new Color("766b4f"),2);
                for(int k=0;k<4;k++) DrawCircle(at+new Vector2(c*(.2f+k*.2f),7),3,new Color(k%2==0?"f4bc54":"e98d87"));
            }
            if(Tool()!="inspect") DrawRect(r,new Color(1,1,1,.13f),false,1);
        }
        if(SelectedPerson()>=0)
        {
            var p=s.People.First(x=>x.Id==SelectedPerson()); var points=new List<Vector2>{Person(p)};points.AddRange(p.Route.Select(Center));
            if(points.Count>1) DrawPolyline(points.ToArray(),new Color("ab563d"),3,true);
        }
        foreach(var p in s.People)
        {
            var at=Person(p); bool chosen=p.Id==SelectedPerson();
            if(chosen) DrawArc(at,10,0,Mathf.Tau,24,new Color("a84931"),3,true);
            DrawCircle(at+new Vector2(1,3),6,new Color("66725a")); DrawCircle(at,5.6f,new Color(p.Preference=="park"?"2d6260":"a64f3c")); DrawCircle(at+new Vector2(0,-5),3.6f,new Color("ffe4b8"));
        }
        foreach(int i in new[]{hover,Selected()}.Where(i=>i>=0).Distinct())
            DrawRect(new Rect2(Origin+new Vector2(i%CityWorld.Width,i/CityWorld.Width)*c,new Vector2(c,c)).Grow(-1),new Color(i==Selected()?"8d4f33":"faf4d9"),false,3);
        Text(new Vector2(20,Size.Y-18),Translate("map_caption"),16,new Color("375044"));
    }
}
