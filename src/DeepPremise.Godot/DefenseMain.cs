using Godot;
using DeepPremise.Core.Defense;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO = System.IO;

namespace DeepPremise.Godot;

public partial class DefenseMain : Control
{
    private DefenseSimulation world = null!;
    private PlaytestRecorder recorder = null!;
    private Dictionary<string,string> translations = new();
    private Dictionary<string,string> english = new();
    private readonly Dictionary<string,Button> commands = new();
    private bool korean = true, paused = true;
    private double elapsed, debugElapsed;
    private int speed = 1;
    private Label summary = null!, instructions = null!, journal = null!, council = null!;
    private DefenseMap map = null!;
    private VBoxContainer panel = null!;
    private string root = "", save = "";
    private string T(string key) => korean && translations.TryGetValue(key, out var value) ? value : english.GetValueOrDefault(key,key);
    public override void _Ready()
    {
        korean = !OS.GetCmdlineUserArgs().Contains("--language=en");
        root = ProjectSettings.GlobalizePath("res://artifacts");
        IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save = IO.Path.Combine(OS.GetUserDataDir(), "defense-v6.json");
        translations = JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/defense.ko.json"))!;
        english = JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/defense.en.json"))!;
        Theme = new Theme { DefaultFont = GD.Load<Font>("res://game/assets/NotoSansKR.ttf"), DefaultFontSize = 16 };
        world = IO.File.Exists(save) ? DefenseSimulation.LoadJson(IO.File.ReadAllText(save)) : new DefenseSimulation(3);
        StartRecorder(); Build(); Refresh();
    }
    private void StartRecorder() => recorder = new PlaytestRecorder(IO.Path.Combine(root,"playtests"),world,"0.4.0-defense",korean ? "ko" : "en");
    private void Build()
    {
        commands.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(margin);
        foreach (var edge in new[] { "left","top","right","bottom" }) margin.AddThemeConstantOverride("margin_"+edge,20);
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation",12); margin.AddChild(rows);
        rows.AddChild(new Label { Text = "UNSEEN ORDER  /  " + T("The watched route"),  });
        summary = new Label(); rows.AddChild(summary);
        instructions = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; rows.AddChild(instructions);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; rows.AddChild(body);
        map = new DefenseMap { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, Read = () => world.Observe(), Translate = T };
        body.AddChild(map);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(370,0) }; body.AddChild(scroll);
        panel = new VBoxContainer { CustomMinimumSize = new Vector2(350,0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(panel);
        Button("Pause / Resume [Space]",()=> { paused = !paused; recorder.Record("pause",paused); });
        Button("Speed 1x / 3x",()=> speed = speed == 1 ? 3 : 1);
        Button("English / Korean",()=> { korean = !korean; Build(); });
        for (var i = 0; i < 3; i++)
        {
            var lane = i;
            panel.AddChild(new Label { Text = T(new[]{"North / reed beds","Middle / old crossing","South / stone shore"}[i]) });
            var row = new HBoxContainer(); panel.AddChild(row);
            foreach (var pair in new[]{("Haul","route"),("Guard","guard"),("Decoy (-3)","decoy")})
            { var b = new Button { Text = T(pair.Item1) }; row.AddChild(b); var action = pair.Item2; b.Pressed += () => Act(action,lane); }
        }
        Button("Recruit worker (-12, max 6)",()=>Act("recruit"));
        Button("Repair supply cart (-5)",()=>Act("repair"));
        council = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; panel.AddChild(council);
        Button("Vote: capture",()=>Act("vote",0));
        Button("Vote: deceive and release",()=>Act("vote",1));
        Button("Mark this moment [F8]",()=>recorder.Record("feedback",new { State = world.Observe(), paused, speed }));
        Button("New expedition",()=> { recorder.Dispose(); world = new DefenseSimulation(Random.Shared.Next(1,100000)); StartRecorder(); paused = true; Persist(); });
        journal = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; panel.AddChild(journal);
    }
    private void Button(string text, Action action) { var b = new Button { Text = T(text) }; panel.AddChild(b); commands[text]=b; b.Pressed += () => { action(); Refresh(); }; }
    private void Act(string action,int lane = 0) { var accepted = world.Order(action,lane); recorder.Record("action",new { action,lane,accepted }); recorder.Capture("order"); Persist(); Refresh(); }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Space) { paused = !paused; recorder.Record("pause",paused); }
            if (key.Keycode == Key.F8) recorder.Record("feedback",new { State = world.Observe(),paused,speed });
            Refresh();
        }
    }
    public override void _Process(double delta)
    {
        elapsed += delta; debugElapsed += delta;
        if (elapsed >= .4) { if (!paused) world.Run(speed); elapsed = 0; Refresh(); }
        if (debugElapsed >= 3) { debugElapsed = 0; Persist(); GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","screen.png")); }
        map.QueueRedraw();
    }
    private void Refresh()
    {
        var s = world.Observe();
        commands["Vote: capture"].Disabled = commands["Vote: deceive and release"].Disabled = !s.Council || s.Ended;
        commands["Recruit worker (-12, max 6)"].Disabled = s.Stock < 12 || s.Workers >= 6 || s.Ended;
        commands["Repair supply cart (-5)"].Disabled = s.Stock < 5 || s.Integrity >= 6 || s.Ended;
        summary.Text = $"{T(paused ? "PAUSED" : "RUNNING")}   •   {T("Stock")} {s.Stock}   •   {T("Cart")} {s.Integrity}/6   •   {T("Workers")} {s.Workers}/6   •   {T("Cycles")} {s.Raids}/6   •   {T("Tick")} {s.Tick}   •   {speed}x";
        instructions.Text = T(s.Ended ? (s.Integrity > 0 ? "won" : "lost") : "Keep the cart intact for six cycles. Haul to grow; guard the scout or show it a false route. Space starts time.");
        council.Text = T(s.Council ? "Council: Quartermaster votes CAPTURE (protect supplies). Pathfinder votes DECEIVE (misdirect attackers). Your vote decides 2–1; work continues." : "Council opens when a scout remains nearby. Routine orders do not require votes.");
        journal.Text = T("FIELD RECORD")+"\n"+string.Join("\n",s.Events.AsEnumerable().Reverse().Select(x=> { var p=x.Split('|'); return p[0]+"  "+T(p[1]); }));
    }
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);
        IO.File.WriteAllText(save+".tmp",world.SaveJson()); IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live","context.json"),JsonSerializer.Serialize(new { ProcessId=System.Environment.ProcessId, Version="0.4.0-defense", world.Tick, Paused=paused, Session=recorder.DirectoryPath, State=world.Observe() },new JsonSerializerOptions { WriteIndented=true }));
    }
    public override void _ExitTree() { Persist(); recorder.Dispose(); }
}

public partial class DefenseMap : Control
{
    public Func<DefenseState> Read { get; set; } = null!;
    public Func<string,string> Translate { get; set; } = x=>x;
    public override void _Draw()
    {
        var s=Read(); var font=GetThemeDefaultFont(); var w=Size.X; var h=Size.Y;
        DrawStyleBox(new StyleBoxFlat { BgColor=new Color("14292b"), CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14 },new Rect2(Vector2.Zero,Size));
        for(var lane=0;lane<3;lane++)
        {
            float y=100+lane*(h-160)/3;
            DrawLine(new Vector2(80,y),new Vector2(w-85,y),new Color(lane==s.Route ? "78b995":"354c4b"),lane==s.Route?9:4);
            DrawString(font,new Vector2(30,y-38),Translate(new[]{"North / reed beds","Middle / old crossing","South / stone shore"}[lane]),HorizontalAlignment.Left,-1,17,new Color("c6d9ca"));
            DrawCircle(new Vector2(65,y),23,new Color("56795d"));
            if(s.Guard==lane) { DrawCircle(new Vector2(w*.65f,y-25),12,new Color("8ebbed")); DrawString(font,new Vector2(w*.65f-25,y-45),Translate("Guard"),HorizontalAlignment.Left,-1,14); }
            if(s.Decoy==lane) { DrawRect(new Rect2(w*.5f,y-10,22,22),new Color("d3af68"),false,3); DrawString(font,new Vector2(w*.5f-20,y+43),Translate("False cargo"),HorizontalAlignment.Left,-1,14); }
            if(s.Route==lane) for(int i=0;i<s.Workers;i++)
            {
                float phase=((s.Tick+i*3)%12)/12f; float x=90+(w-200)*phase;
                DrawCircle(new Vector2(x,y+12+(i%2)*15),7,new Color("e4d8b5"));
                DrawRect(new Rect2(x-5,y-7,10,10),new Color("82c794"));
            }
            if(s.ScoutLane==lane)
            {
                var p=new Vector2(w*.42f,y-55); DrawCircle(p,10,new Color("efa775"));
                DrawArc(p,28,0,Mathf.Tau,32,new Color("efa775"),2);
                DrawString(font,p+new Vector2(-32,-38),Translate("Scout watching"),HorizontalAlignment.Left,-1,15,new Color("efa775"));
            }
            if(s.RaidLane==lane)
            {
                float x=w-100-(float)((s.Tick%120-85)/20.0)*(w-220);
                for(int i=0;i<3;i++) DrawCircle(new Vector2(x+i*15,y-15),9,new Color("e56f63"));
                DrawString(font,new Vector2(w*.5f,y-65),Translate("Attack incoming"),HorizontalAlignment.Left,-1,18,new Color("e56f63"));
            }
        }
        DrawRect(new Rect2(w-80,70,55,h-180),new Color("455d57"));
        DrawString(font,new Vector2(w-100,h-80),Translate("Supply cart"),HorizontalAlignment.Left,-1,17);
        DrawString(font,new Vector2(30,h-35),Translate("Green: workers + cargo   Blue: guard   Amber: scout   Red: attackers"),HorizontalAlignment.Left,-1,15,new Color("c6d9ca"));
    }
}
