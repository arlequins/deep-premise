using Godot;
using DeepPremise.Core.FirstLight;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO=System.IO;

namespace DeepPremise.Godot;

public partial class FirstLightMain : Control
{
    private LightWorld world=null!;
    private LightState state=null!;
    private PlaytestRecorder recorder=null!;
    private Dictionary<string,string> words=new();
    private LightCanvas canvas=null!;
    private Label objective=null!,caption=null!;
    private Button pauseButton=null!,retry=null!;
    private bool korean=true,paused,smoke;
    private string root="",save="";
    private double timer,saveClock,clock;
    private int smokeStage;
    private string T(string key)=>words.GetValueOrDefault(key,key);
    public override void _Ready()
    {
        korean=!OS.GetCmdlineUserArgs().Contains("--language=en");smoke=OS.GetCmdlineUserArgs().Contains("--smoke-light");
        root=ProjectSettings.GlobalizePath("res://artifacts");IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save=smoke?IO.Path.Combine(root,"live","light-smoke.json"):IO.Path.Combine(OS.GetUserDataDir(),"type10-light-v12.json");
        bool fresh=OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if(fresh&&!smoke&&IO.File.Exists(save))IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        world=!fresh&&!smoke&&IO.File.Exists(save)?LightWorld.LoadJson(IO.File.ReadAllText(save)):new LightWorld();
        Theme=new Theme{DefaultFont=new FontVariation{BaseFont=GD.Load<Font>("res://game/assets/NotoSansKR.ttf"),VariationEmbolden=.6f},DefaultFontSize=20};
        Theme.SetColor("font_color","Label",new Color("dedbcf"));Theme.SetColor("font_color","Button",new Color("dedbcf"));
        RecordSession();state=world.Observe();Build();Refresh();Persist();
    }
    private void RecordSession()=>recorder=new PlaytestRecorder(IO.Path.Combine(root,"playtests"),world,"type10-0.1",korean?"ko":"en");
    private Button MakeButton(string text,Action action)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(90,42),FocusMode=FocusModeEnum.None};
        foreach(string mode in new[]{"normal","hover","pressed"})b.AddThemeStyleboxOverride(mode,new StyleBoxFlat{BgColor=new Color(mode=="normal"?"171e23":"293137"),CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8,ContentMarginLeft=15,ContentMarginRight=15});
        b.Pressed+=()=>{action();Refresh();};return b;
    }
    private void Build()
    {
        words=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/light."+(korean?"ko":"en")+".json"))!;
        foreach(var c in GetChildren()){RemoveChild(c);c.QueueFree();}
        var bg=new ColorRect{Color=new Color("0a0e13"),MouseFilter=MouseFilterEnum.Ignore};bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        var margin=new MarginContainer();margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(margin);
        foreach(string edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,26);
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",10);margin.AddChild(column);
        var bar=new HBoxContainer();bar.AddThemeConstantOverride("separation",10);column.AddChild(bar);
        var title=new Label{Text=T("title"),SizeFlagsHorizontal=SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",26);bar.AddChild(title);
        pauseButton=MakeButton(T("pause"),()=>{paused=!paused;recorder.Record("action",new{Action="pause",paused});});bar.AddChild(pauseButton);
        bar.AddChild(MakeButton(korean?"EN":"한국어",()=>{korean=!korean;Build();}));
        retry=MakeButton(T("retry"),Restart);bar.AddChild(retry);
        objective=new Label{HorizontalAlignment=HorizontalAlignment.Center};objective.AddThemeColorOverride("font_color",new Color("d8c59b"));column.AddChild(objective);
        canvas=new LightCanvas{Read=()=>state,Aim=Aim,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(640,400)};column.AddChild(canvas);
        caption=new Label{HorizontalAlignment=HorizontalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,58)};column.AddChild(caption);
    }
    private void Restart()
    {
        Persist();if(IO.File.Exists(save))IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        recorder.Record("action",new{Action="restart"});recorder.Dispose();world=new LightWorld();RecordSession();paused=false;state=world.Observe();Persist();
    }
    private void Aim(double x,double y,bool on)
    {
        if(state.Won||state.Lost)return;
        world.Aim(x,y,on);recorder.Record("action",new{Action="aim",x,y,on});Refresh();
    }
    private void Refresh()
    {
        state=world.Observe();int adults=state.Entities.Count(e=>e.Alive&&e.Kind=="life"&&e.Meals>=3);
        objective.Text=state.Won?T("won"):state.Lost?T("lost"):adults>=3?string.Format(T("release_goal"),Math.Max(0,24-state.IndependentTicks/10)):string.Format(T("goal"),Math.Min(3,adults));
        string hint=state.Tick<45?"first_hint":!state.Entities.Any(e=>e.Kind=="life")?"egg_hint":state.HunterArrived?"hunter_hint":"growth_hint";
        var last=state.Events.LastOrDefault(e=>e.Key is "birth" or "grown" or "hunter" or "taken" or "starved" or "new_seed");
        if(last!=null&&state.Tick-last.Tick<65)hint=last.Key;
        if(state.HunterArrived&&state.Entities.Any(e=>e.Kind=="hunter"&&LightWorld.IsLit(state,e)))hint="held_hint";
        caption.Text=T(state.Won?"won_hint":state.Lost?"lost_hint":adults>=3?"release_hint":hint);
        pauseButton.Text=T(paused?"resume":"pause");retry.Visible=state.Won||state.Lost;canvas.QueueRedraw();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is not InputEventKey{Pressed:true,Echo:false} key)return;
        if(key.Keycode==Key.Space){paused=!paused;recorder.Record("action",new{Action="pause",paused});}
        if(key.Keycode==Key.F8){recorder.Record("action",new{Action="feedback",state.LightX,state.LightY,state.LightOn});recorder.Capture("feedback");Persist();}Refresh();
    }
    public override void _Process(double delta)
    {
        timer+=delta;saveClock+=delta;clock+=delta;
        if(timer>=.1){int ticks=Math.Min(10,(int)(timer/.1));timer-=ticks*.1;if(!paused)world.Run(ticks);Refresh();}
        canvas.Clock+=(float)delta;canvas.QueueRedraw();
        if(saveClock>=3){saveClock=0;recorder.Capture("interval");Persist();GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","type10-screen.png"));}
        if(smoke)
        {
            if(clock>.6&&smokeStage==0){Input.ParseInputEvent(new InputEventMouseMotion{Position=canvas.Point(.82,.77),GlobalPosition=canvas.Point(.82,.77)});world.Run(200);smokeStage++;}
            if(clock>1.3&&smokeStage==1){var p=world.Observe().Entities.First(e=>e.Kind=="life");Input.ParseInputEvent(new InputEventMouseMotion{Position=canvas.Point(p.X,p.Y),GlobalPosition=canvas.Point(p.X,p.Y)});smokeStage++;}
            if(clock>3){bool ok=state.Entities.Any(e=>e.Kind=="life"&&LightWorld.IsLit(state,e))&&state.Memories.Any(m=>m.Entity.Kind=="life");GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"light-ko.png":"light-en.png"));GD.Print("FIRST LIGHT NATIVE INPUT "+(ok?"PASS":"FAIL"));GetTree().Quit(ok?0:1);}
        }
    }
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);IO.File.WriteAllText(save+".tmp",world.SaveJson());IO.File.Move(save+".tmp",save,true);
        IO.File.WriteAllText(IO.Path.Combine(root,"live",smoke?"type10-smoke-context.json":"type10-context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="type10-0.1",world.Tick,Paused=paused,Session=recorder.DirectoryPath,State=world.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree(){Persist();recorder.Dispose();}
}

public partial class LightCanvas : Control
{
    public Func<LightState> Read=null!;
    public Action<double,double,bool> Aim=null!;
    public float Clock;
    private Rect2 Field
    {
        get{float h=Math.Min(Size.Y-20,(Size.X-20)/1.6f);return new Rect2((Size-new Vector2(h*1.6f,h))/2,new Vector2(h*1.6f,h));}
    }
    private Vector2 At(double x,double y)=>Field.Position+new Vector2((float)x,(float)y)*Field.Size;
    public Vector2 Point(double x,double y)=>GlobalPosition+At(x,y);
    public override void _Ready(){MouseExited+=()=>{var s=Read();Aim(s.LightX,s.LightY,false);};}
    public override void _GuiInput(InputEvent input)
    {
        if(input is not InputEventMouseMotion motion)return;
        var uv=(motion.Position-Field.Position)/Field.Size;Aim(uv.X,uv.Y,Field.HasPoint(motion.Position));
    }
    public override void _Draw()
    {
        var s=Read();var field=Field;
        DrawStyleBox(new StyleBoxFlat{BgColor=new Color("0c1319"),CornerRadiusTopLeft=28,CornerRadiusTopRight=28,CornerRadiusBottomLeft=28,CornerRadiusBottomRight=28,BorderColor=new Color("1c282c"),BorderWidthBottom=1,BorderWidthTop=1,BorderWidthLeft=1,BorderWidthRight=1},field);
        for(int i=0;i<70;i++)DrawCircle(At(.03+(i*37%94)/100.0,.03+(i*23%93)/100.0),i%3==0?1.5f:1,new Color("243139"));
        if(s.LightOn)
        {
            var p=At(s.LightX,s.LightY);float radius=(float)LightWorld.Radius*field.Size.Y;
            for(int i=24;i>=1;i--)DrawCircle(p,radius*(1+i*.014f),new Color(.85f,.81f,.65f,.003f+(24-i)*.00065f));
            DrawCircle(p,radius,new Color("333a35"));
            DrawArc(p,radius,0,Mathf.Tau,100,new Color("777767"),1,true);
            DrawCircle(p,3,new Color("f7eccb"));
        }
        foreach(var memory in s.Memories.Where(m=>!s.Entities.Any(e=>e.Id==m.Entity.Id&&LightWorld.IsLit(s,e))))DrawEntity(memory.Entity,.18f);
        foreach(var entity in s.Entities.Where(e=>e.Alive&&LightWorld.IsLit(s,e)))DrawEntity(entity,1);
        foreach(var e in s.Events.Where(e=>s.Tick-e.Tick<55&&e.Key is "birth" or "new_seed" or "hunter" or "taken" or "grown"))
        {
            float elapsed=(s.Tick-e.Tick)/55f;
            DrawArc(At(e.X,e.Y),16+elapsed*40,0,Mathf.Tau,48,new Color(e.Key is "hunter" or "taken"?"d58b83":"9abeb1") with{A=(1-elapsed)*.65f},2,true);
        }
        if(s.Won)
        {
            foreach(var entity in s.Entities.Where(e=>e.Alive))DrawEntity(entity,.65f);
            for(int i=0;i<12;i++)DrawCircle(At(.15+(i*7%70)/100.0,.18+(i*13%64)/100.0),2+Mathf.Sin(Clock+i),new Color("cfdfa6"));
        }
    }
    private void DrawEntity(LightEntity e,float alpha)
    {
        if(!e.Alive)return;
        var p=At(e.X,e.Y);Color C(string hex)=>new Color(hex) with{A=alpha};
        if(e.Kind=="egg")
        {
            DrawCircle(p,13,C("d7d5b7"));DrawArc(p,20,-1,2.5f,24,C("8a9584"),2,true);
            DrawLine(p+new Vector2(-3,-8),p+new Vector2(3,0),C("858879"),2);DrawLine(p+new Vector2(3,0),p+new Vector2(-2,7),C("858879"),2);
        }
        if(e.Kind=="plant")
        {
            if(e.Age<70){DrawColoredPolygon(new[]{p+new Vector2(-15,6),p+new Vector2(-9,-8),p+new Vector2(6,-13),p+new Vector2(16,7)},C("7d8577"));}
            else if(e.Age<180){for(int i=0;i<4;i++)DrawCircle(p+new Vector2((i-1.5f)*8,Mathf.Sin(i)*5),9,C("729779"));}
            else
            {
                bool tree=e.Age>=850;float scale=tree?1.5f:1;
                DrawLine(p+new Vector2(0,17),p-new Vector2(0,25*scale),C("8b9580"),5,true);
                for(int i=0;i<5;i++){float a=i*Mathf.Tau/5;DrawCircle(p+new Vector2(Mathf.Cos(a)*17,Mathf.Sin(a)*16-15)*scale,tree?17:10,C(tree?"69867a":"9db298"));}
                for(int i=0;i<e.Fruit;i++)DrawCircle(p+new Vector2((i-1.5f)*8,8),4,C("e2be79"));
                if(tree)DrawArc(p,50,0,Mathf.Tau,48,C("385b51"),1,true);
            }
        }
        if(e.Kind=="life")
        {
            float r=e.Meals>=3?16:10;
            DrawCircle(p,r,C(e.Meals>=3?"cad6a4":"9bc8bc"));
            for(int i=0;i<3;i++)DrawLine(p+new Vector2((i-1)*6,r-3),p+new Vector2((i-1)*9,r+7),C("93b3a1"),3,true);
            DrawCircle(p+new Vector2(-4,-2),2,C("233b39"));DrawCircle(p+new Vector2(4,-2),2,C("233b39"));
            if(e.Hunger>500)DrawArc(p,r+6,-2.5f,-.6f,16,C("d59b80"),2,true);
        }
        if(e.Kind=="hunter")
        {
            DrawColoredPolygon(new[]{p+new Vector2(-26,5),p+new Vector2(-14,-15),p+new Vector2(0,-9),p+new Vector2(13,-18),p+new Vector2(29,4),p+new Vector2(0,16)},C("966c70"));
            DrawCircle(p+new Vector2(-7,-1),3,C("f2b99a"));DrawCircle(p+new Vector2(8,-1),3,C("f2b99a"));
        }
    }
}
