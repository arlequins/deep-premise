using Godot;
using DeepPremise.Core.Sixfold;
using DeepPremise.Core.Diagnostics;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IO=System.IO;

namespace DeepPremise.Godot;

public partial class SixfoldMain : Control
{
    private SixfoldRun run=null!;
    private SixfoldState state=null!;
    private PlaytestRecorder recorder=null!;
    private Dictionary<string,string> words=new();
    private bool korean=true,paused,smoke;
    private int selected=-1,pending=-1,speed=2,smokeStage;
    private string root="",save="",profilePath="",lastPhase="";
    private double elapsed,saveClock,clock;
    private SixfoldProfile profile=new();
    private Label header=null!,detail=null!,message=null!;
    private HBoxContainer choices=null!;
    private Button primary=null!,rest=null!,upgrade=null!,reroll=null!,speedButton=null!;
    private readonly List<Button> slots=new();
    private readonly List<Button> choiceButtons=new();
    private SixfoldArena arena=null!;
    private SixfoldBoardOverlay boardOverlay=null!;
    private int hoveredOffer=-1;
    private string T(string key)=>words.GetValueOrDefault(key,key);
    public override void _Ready()
    {
        korean=!OS.GetCmdlineUserArgs().Contains("--language=en");smoke=OS.GetCmdlineUserArgs().Contains("--smoke-sixfold");
        root=OS.HasFeature("editor")?ProjectSettings.GlobalizePath("res://artifacts"):IO.Path.Combine(OS.GetUserDataDir(),"artifacts");IO.Directory.CreateDirectory(IO.Path.Combine(root,"live"));
        save=smoke?IO.Path.Combine(root,"live","sixfold-smoke.json"):IO.Path.Combine(OS.GetUserDataDir(),"sixfold-v13.json");
        profilePath=smoke?IO.Path.Combine(root,"live","sixfold-profile-smoke.json"):IO.Path.Combine(OS.GetUserDataDir(),"sixfold-profile.json");
        if(!smoke&&IO.File.Exists(profilePath))profile=JsonSerializer.Deserialize<SixfoldProfile>(IO.File.ReadAllText(profilePath))??new();
        bool fresh=OS.GetCmdlineUserArgs().Contains("--fresh-run");
        if(fresh&&!smoke&&IO.File.Exists(save))IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");
        run=!fresh&&!smoke&&IO.File.Exists(save)?SixfoldRun.LoadJson(IO.File.ReadAllText(save)):new SixfoldRun(smoke?101:(uint)Random.Shared.Next(1,int.MaxValue));
        if(smoke&&OS.GetCmdlineUserArgs().Contains("--smoke-replace"))
        {
            var fixture=run.Observe();fixture.Phase="draft";fixture.Offers=new(){"venom","shell","eye"};
            fixture.Body=Enumerable.Range(0,6).Select(i=>(Organ?)new Organ{Kind=i==0?"jaw":"stomach"}).ToList();
            run=SixfoldRun.LoadJson(JsonSerializer.Serialize(fixture));
        }
        RecordSession();state=run.Observe();
        Theme=new Theme{DefaultFont=new FontVariation{BaseFont=GD.Load<Font>("res://game/assets/NotoSansKR.ttf"),VariationEmbolden=.7f},DefaultFontSize=18};
        Theme.SetColor("font_color","Label",new Color("f3eee4"));Theme.SetColor("font_color","Button",new Color("f3eee4"));
        Build();Refresh();Persist();
    }
    private void RecordSession()=>recorder=new PlaytestRecorder(IO.Path.Combine(root,"playtests"),run,"0.10.0",korean?"ko":"en");
    private StyleBoxFlat Style(string color)=>new(){BgColor=new Color(color),CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=8,ContentMarginBottom=8};
    private Button Button(string text,Action action)
    {
        var b=new Button{Text=text,FocusMode=FocusModeEnum.None,CustomMinimumSize=new Vector2(100,46)};
        b.AddThemeStyleboxOverride("normal",Style("27333e"));b.AddThemeStyleboxOverride("hover",Style("3b4d57"));b.AddThemeStyleboxOverride("pressed",Style("506352"));
        b.AddThemeStyleboxOverride("disabled",Style("222e39"));b.AddThemeColorOverride("font_disabled_color",new Color("c6cbd0"));
        b.Pressed+=()=>{action();Refresh();};return b;
    }
    private void Build()
    {
        words=JsonSerializer.Deserialize<Dictionary<string,string>>(global::Godot.FileAccess.GetFileAsString("res://game/sixfold."+(korean?"ko":"en")+".json"))!;
        foreach(var c in GetChildren()){RemoveChild(c);c.QueueFree();}slots.Clear();choiceButtons.Clear();lastPhase="";hoveredOffer=-1;
        var bg=new ColorRect{Color=new Color("101923"),MouseFilter=MouseFilterEnum.Ignore};bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        var margin=new MarginContainer();margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(margin);
        foreach(string e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,24);
        var col=new VBoxContainer();col.AddThemeConstantOverride("separation",10);margin.AddChild(col);
        var bar=new HBoxContainer();bar.AddThemeConstantOverride("separation",12);col.AddChild(bar);
        var title=new Label{Text=T("title")};title.AddThemeFontSizeOverride("font_size",25);bar.AddChild(title);
        header=new Label{SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalAlignment=HorizontalAlignment.Center};bar.AddChild(header);
        speedButton=Button("2×",()=>{speed=speed==2?1:2;Record("speed",new{speed});});bar.AddChild(speedButton);
        bar.AddChild(Button(korean?"EN":"한국어",()=>{korean=!korean;Build();}));
        arena=new SixfoldArena{Read=()=>state,T=T,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(0,270)};col.AddChild(arena);
        var organs=new GridContainer{Columns=3,SizeFlagsHorizontal=SizeFlags.ShrinkCenter};organs.AddThemeConstantOverride("separation",10);col.AddChild(organs);
        organs.AddThemeConstantOverride("h_separation",22);organs.AddThemeConstantOverride("v_separation",18);
        for(int i=0;i<6;i++){int slot=i;var b=Button("",()=>Slot(slot));b.CustomMinimumSize=new Vector2(300,76);slots.Add(b);organs.AddChild(b);}
        detail=new Label{HorizontalAlignment=HorizontalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,32)};col.AddChild(detail);
        choices=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center,CustomMinimumSize=new Vector2(0,166)};choices.AddThemeConstantOverride("separation",14);col.AddChild(choices);
        var footer=new HBoxContainer();footer.AddThemeConstantOverride("separation",10);col.AddChild(footer);
        message=new Label{SizeFlagsHorizontal=SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart};footer.AddChild(message);
        rest=Button(T("rest"),()=>Act("rest",run.Rest));footer.AddChild(rest);
        upgrade=Button(T("upgrade"),()=>Act("upgrade",()=>run.Upgrade(selected),new{selected}));footer.AddChild(upgrade);
        reroll=Button(T("reroll"),()=>Act("reroll",run.Reroll));footer.AddChild(reroll);
        primary=Button(T("fight"),Primary);primary.CustomMinimumSize=new Vector2(180,50);primary.AddThemeStyleboxOverride("normal",Style("735344"));footer.AddChild(primary);
        boardOverlay=new SixfoldBoardOverlay{Read=()=>state,Slots=slots,Selected=()=>selected,MouseFilter=MouseFilterEnum.Ignore};boardOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(boardOverlay);
    }
    private void Record(string action,object data)=>recorder.Record("action",new{Action=action,Detail=data});
    private void Act(string key,Func<bool> action,object? data=null){bool ok=action();Record(key,new{Accepted=ok,Data=data});recorder.Capture(key);Refresh();Persist();}
    private void Slot(int slot)
    {
        if(state.Phase is not ("draft" or "prepare"))return;
        if(pending>=0){int offer=pending;pending=-1;selected=slot;Act("take",()=>run.Take(offer,slot),new{offer,slot});return;}
        if(selected>=0&&selected!=slot){int first=selected;Act("swap",()=>run.Swap(first,slot),new{first,slot});selected=-1;}
        else selected=selected==slot?-1:slot;
    }
    private void Choose(int index)
    {
        if(state.Phase=="starter"){Act("starter",()=>run.ChooseStarter(index),new{index});return;}
        if(state.Phase=="relic"){Act("relic",()=>run.ChooseRelic(index),new{index});return;}
        if(state.Phase=="prepare"){string route=index==0?"safe":"elite";Act("route",()=>run.SetRoute(route),new{route});return;}
        if(state.Phase!="draft")return;
        string kind=state.Offers[index];int slot=state.Body.FindIndex(o=>o?.Kind==kind&&o.Level<3);
        if(slot<0)slot=state.Body.FindIndex(o=>o==null);
        if(slot>=0){Act("take",()=>run.Take(index,slot),new{index,slot});selected=slot;pending=-1;}
        else{pending=index;selected=-1;}
    }
    private void Primary()
    {
        if(state.Phase=="prepare"){selected=-1;Act("fight",run.Fight);}
        else if(state.Phase=="victory")Act("continue",run.Continue);
        else if(state.Phase is "lost" or "won")NewRun();
        else if(state.Phase=="battle"){paused=!paused;Record("pause",new{paused});}
    }
    private void NewRun()
    {
        Persist();IO.File.Copy(save,save+"."+Guid.NewGuid().ToString("N")+".bak");recorder.Dispose();
        run=new SixfoldRun((uint)Random.Shared.Next(1,int.MaxValue));RecordSession();selected=pending=-1;paused=false;lastPhase="";Refresh();Persist();
    }
    private string Stats(string kind,int level,int slot=-1)
    {
        var spec=SixfoldRun.Catalog.First(c=>c.Id==kind);
        int hearts=slot<0?0:SixfoldRun.Neighbors(slot).Sum(n=>state.Body[n]?.Kind=="heart"?state.Body[n]!.Level:0);
        int power=spec.Power+(level-1)*3+hearts*2;
        string effect=kind switch{"jaw" or "spike" or "cannon" or "leechfang" or "lance"=>T("effect_attack")+" "+(power+(state.Relics.Contains("fang")?3:0)),"venom"=>T("poison")+" +"+(level*2+1+(state.Relics.Contains("crown")?2:0)),"stomach"=>T("effect_heal")+" +"+power,"shell" or "lung"=>T("effect_shield")+" +"+power,"brood"=>T("effect_drone")+" +"+level,"heart"=>T("effect_speed")+" +"+(level*.2).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"s","eye"=>T("effect_double"),"catalyst"=>T("effect_catalyst"),"capacitor"=>T("effect_capacitor"),"frost"=>T("effect_frost"),_=>""};
        return effect+(spec.Interval==0?"":"  /  "+(Math.Max(6,spec.Interval-hearts*2-(slot>=0&&state.Body[slot]?.Adapted==true?3:0))/10.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"s");
    }
    private string OrganText(string kind)=>T(kind)+"\n"+Stats(kind,1);
    private void Refresh()
    {
        state=run.Observe();
        if(state.Phase!="draft"||pending>=state.Offers.Count)pending=-1;
        header.Text=string.Format(T("header"),state.Floor,state.Bosses,state.Gold);
        speedButton.Text=speed+"×";speedButton.Disabled=state.Phase!="battle";
        for(int i=0;i<6;i++)
        {
            var o=state.Body[i];var b=slots[i];b.Text=o==null?"+":T(o.Kind)+(o.Adapted?" ↑":"")+" "+new string('★',o.Level)+"\n"+Stats(o.Kind,o.Level,i);
            b.Disabled=state.Phase is not ("prepare" or "draft");b.Modulate=i==selected?new Color("ffe0a1"):Colors.White;
            if(o!=null&&state.Phase=="battle"&&o.Clock<3&&o.Activations>0)b.Modulate=new Color(SixfoldRun.Catalog.First(c=>c.Id==o.Kind).Color);
            b.TooltipText=o==null?T("empty"):T(o.Kind+"_desc");
        }
        var organ=selected>=0?state.Body[selected]:null;
        detail.Text=pending>=0?string.Format(T("replace_hint"),T(state.Offers[pending])):organ!=null?T(organ.Kind)+"  "+Stats(organ.Kind,organ.Level,selected)+(organ.Level<3?"   →   "+Stats(organ.Kind,organ.Level+1,selected):""):T(state.Phase=="battle"?"battle_hint":"body_hint");
        rest.Visible=upgrade.Visible=state.Phase=="prepare";
        rest.Disabled=state.Gold<7||state.Health==state.MaxHealth;
        upgrade.Disabled=organ==null||organ.Level==3||state.Gold<7+organ.Level*3;upgrade.Text=organ==null?T("upgrade"):string.Format(T("upgrade_cost"),7+organ.Level*3);
        reroll.Visible=state.Phase=="draft";reroll.Text=string.Format(T("reroll"),4+state.Rerolls*2);reroll.Disabled=state.Gold<4+state.Rerolls*2;
        primary.Visible=state.Phase is "prepare" or "battle" or "victory" or "lost" or "won";
        primary.Text=T(state.Phase switch{"battle"=>paused?"resume":"pause","victory"=>state.Floor==12?"finish":"claim","lost" or "won"=>"new_run",_=>"fight"});
        message.Text=T(state.Phase+"_hint");
        string phaseKey=state.Phase+":"+state.Floor+":"+string.Join(",",state.Offers)+":"+pending+":"+state.Route;
        if(lastPhase!=phaseKey)
        {
            lastPhase=phaseKey;foreach(var c in choices.GetChildren()){choices.RemoveChild(c);c.QueueFree();}choiceButtons.Clear();
            string[] labels=state.Phase switch{
                "starter"=>new[]{T("starter0"),T("starter1"),T("starter2")},
                "draft"=>state.Offers.Select(k=>OrganText(k)+"\n\n"+(state.Body.FirstOrDefault(o=>o?.Kind==k&&o.Level<3) is {} match ? new string('★',match.Level)+" → "+new string('★',match.Level+1):T("take"))).ToArray(),
                "relic"=>state.RelicOffers.Select(k=>T("relic_"+k)+"\n\n"+T("relic_"+k+"_desc")).ToArray(),
                "prepare"=>state.Floor%4==0?new[]{T("boss_preview")+"\n\n"+T(state.Enemy.Kind+"_desc")}:new[]{T("safe_route"),T("elite_route")},
                "victory"=>new[]{T(state.Floor%4==0?"boss_victory":"victory")+"\n\n"+string.Format(T("reward"),8+(state.Enemy.Elite?8:0)+(state.Floor%4==0?8:0))},
                "lost"=>new[]{T("lost")+"\n\n"+string.Format(T("record"),state.Floor,profile.Best)},
                "won"=>new[]{T("won")+"\n\n"+T("won_desc")},_=>Array.Empty<string>()};
            for(int i=0;i<labels.Length;i++)
            {
                int index=i;var b=Button(labels[i],()=>Choose(index));b.CustomMinimumSize=new Vector2(labels.Length==3?370:labels.Length==2?430:690,156);b.AddThemeFontSizeOverride("font_size",18);
                b.Disabled=state.Phase is "victory" or "lost" or "won" || state.Phase=="prepare"&&state.Floor%4==0;
                if(state.Phase=="prepare"&&((index==0&&state.Route=="safe")||(index==1&&state.Route=="elite")))b.AddThemeStyleboxOverride("normal",Style("435349"));
                choices.AddChild(b);choiceButtons.Add(b);
                if(state.Phase=="draft")
                {
                    string kind=state.Offers[i];b.TooltipText=T(kind+"_desc");
                    b.MouseEntered+=()=>{hoveredOffer=index;};b.MouseExited+=()=>{hoveredOffer=-1;};
                    var glyph=new SixfoldGlyph{Kind=kind,Position=new Vector2(14,14),MouseFilter=MouseFilterEnum.Ignore};b.AddChild(glyph);
                }
            }
        }
        if(state.Phase=="draft"&&hoveredOffer>=0&&hoveredOffer<state.Offers.Count)
        {
            string kind=state.Offers[hoveredOffer];int target=state.Body.FindIndex(o=>o?.Kind==kind&&o.Level<3);if(target<0)target=state.Body.FindIndex(o=>o==null);
            if(target>=0){slots[target].Modulate=new Color("ffe0a1");var existing=state.Body[target];detail.Text=existing==null?T(kind)+" → +":Stats(kind,existing.Level,target)+"   →   "+Stats(kind,existing.Level+1,target);}
        }
        boardOverlay?.QueueRedraw();
        arena.QueueRedraw();
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is not InputEventKey{Pressed:true,Echo:false}key)return;
        if(key.Keycode==Key.Space&&state.Phase=="battle"){paused=!paused;Record("pause",new{paused});}
        if(key.Keycode==Key.F8){Record("feedback",new{state.Floor,state.Phase,selected,pending});recorder.Capture("feedback");Persist();}
    }
    public override void _Process(double delta)
    {
        clock+=delta;elapsed+=delta;saveClock+=delta;
        if(elapsed>=.1){int ticks=Math.Min(10,(int)(elapsed/.1));elapsed-=ticks*.1;if(!paused)run.Run(ticks*speed);Refresh();}
        arena.Clock+=(float)delta;arena.QueueRedraw();
        if(saveClock>3){saveClock=0;Persist();GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live","sixfold-screen.png"));}
        if(smoke)
        {
            if(clock>.5&&smokeStage==0){NativeClick(choiceButtons[0]);smokeStage++;}
            if(clock>.9&&smokeStage==1){NativeClick(OS.GetCmdlineUserArgs().Contains("--smoke-replace")?slots[1]:choiceButtons[0]);smokeStage++;}
            if(clock>1.3&&smokeStage==2){NativeClick(primary);smokeStage++;}
            if(clock>5){bool ok=state.Phase is "battle" or "victory"&&state.Body.Count(o=>o!=null)>=2&&state.Events.Count>0;GetViewport().GetTexture().GetImage().SavePng(IO.Path.Combine(root,"live",korean?"sixfold-ko.png":"sixfold-en.png"));GD.Print("SIXFOLD NATIVE INPUT "+(ok?"PASS":"FAIL"));GetTree().Quit(ok?0:1);}
        }
    }
    private static void NativeClick(Button b)
    {var p=b.GetGlobalRect().GetCenter();Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});}
    private void Persist()
    {
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(save)!);IO.File.WriteAllText(save+".tmp",run.SaveJson());IO.File.Move(save+".tmp",save,true);
        profile.Best=Math.Max(profile.Best,run.Observe().Floor);IO.File.WriteAllText(profilePath,JsonSerializer.Serialize(profile));
        IO.File.WriteAllText(IO.Path.Combine(root,"live",smoke?"sixfold-smoke-context.json":"sixfold-context.json"),JsonSerializer.Serialize(new{ProcessId=System.Environment.ProcessId,Version="0.10.0",run.Tick,Session=recorder.DirectoryPath,State=run.Observe()},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _ExitTree(){Persist();recorder.Dispose();}
    private sealed class SixfoldProfile{public int Best{get;set;}}
}

public partial class SixfoldArena : Control
{
    public Func<SixfoldState> Read=null!;public Func<string,string>T=x=>x;public float Clock;
    private void Text(Vector2 p,string text,int size,Color color)=>DrawString(GetThemeDefaultFont(),p,text,HorizontalAlignment.Left,-1,size,color);
    private void Bar(Vector2 p,float width,float value,string color)
    {DrawStyleBox(new StyleBoxFlat{BgColor=new Color("17202a"),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6},new Rect2(p,new Vector2(width,15)));if(value>0)DrawRect(new Rect2(p,new Vector2(width*Math.Clamp(value,0,1),15)),new Color(color));}
    public override void _Draw()
    {
        var s=Read();DrawStyleBox(new StyleBoxFlat{BgColor=new Color("1d2935"),CornerRadiusTopLeft=24,CornerRadiusTopRight=24,CornerRadiusBottomLeft=24,CornerRadiusBottomRight=24},new Rect2(Vector2.Zero,Size));
        for(int i=0;i<12;i++){var p=new Vector2(Size.X/2-132+i*24,24);DrawCircle(p,i%4==3?6:4,new Color(i<s.Floor-1?"e3c08c":i==s.Floor-1?"f1ece0":"495361"));}
        Vector2 left=new(Size.X*.25f,Size.Y*.57f),right=new(Size.X*.75f,Size.Y*.57f);
        DrawLine(new Vector2(40,Size.Y*.83f),new Vector2(Size.X-40,Size.Y*.83f),new Color("38434a"),2);
        var hurt=s.Events.LastOrDefault(e=>e.Kind=="hurt"&&e.Amount>0);float shake=hurt!=null&&s.Tick-hurt.Tick<3?Mathf.Sin(Clock*80)*5:0;
        Creature(left+new Vector2(shake,0),false,s);Creature(right,true,s);
        float barWidth=Math.Min(320,Size.X*.3f);
        Bar(new Vector2(left.X-barWidth/2,62),barWidth,(float)s.Health/s.MaxHealth,"8fb99b");Text(new Vector2(left.X-barWidth/2,54),T("you")+"  "+s.Health+" / "+s.MaxHealth+(s.Shield>0?"  +"+s.Shield:""),20,new Color("e8efda"));
        Bar(new Vector2(right.X-barWidth/2,62),barWidth,(float)s.Enemy.Health/s.Enemy.MaxHealth,"ce898a");Text(new Vector2(right.X-barWidth/2,54),T(s.Enemy.Kind)+(s.Enemy.Trait!="none"?" · "+T("trait_"+s.Enemy.Trait):"")+"  "+s.Enemy.Health+" / "+s.Enemy.MaxHealth,20,new Color("f0ddd6"));
        if(s.Phase=="prepare")Text(new Vector2(right.X-barWidth/2,100),T(s.Enemy.Kind+"_short"),17,new Color("c4c3c7"));
        if(s.Phase=="battle")
        {
            Bar(right+new Vector2(-70,77),140,(float)s.Enemy.Clock/s.Enemy.Interval,"cc9b74");
            foreach(var e in s.Events.TakeLast(12).Where(e=>s.Tick-e.Tick<9&&e.Amount>0))
            {
                bool friendly=e.Kind is "heal" or "shield" or "cleanse" or "block";
                bool onPlayer=friendly||e.Kind=="hurt";var p=(onPlayer?left:right)+new Vector2((e.Slot+1)*11-25,-50-(s.Tick-e.Tick)*5);
                Text(p,(friendly?"+":"−")+e.Amount,e.Kind=="critical"?30:23,new Color(friendly?"a6dab0":e.Kind is "poison" or "venom"?"cdb1ec":"f2d7b2"));
                if(e.Kind is "hit" or "critical" or "reflect"&&s.Tick-e.Tick<2)DrawLine(left+new Vector2(55,-10),right+new Vector2(-55,-10),new Color("eac78d"),e.Kind=="critical"?4:2,true);
            }
            if(s.Enemy.Poison>0)Text(right+new Vector2(-32,115),T("poison")+" "+s.Enemy.Poison,18,new Color("bea0dc"));
        }
        if(s.Relics.Count>0)Text(new Vector2(20,Size.Y-15),string.Join("  ·  ",s.Relics.Select(r=>T("relic_"+r))),16,new Color("dac694"));
    }
    private void Creature(Vector2 p,bool enemy,SixfoldState s)
    {
        float bob=Mathf.Sin(Clock*(enemy?2.5f:2))*3; p.Y+=bob;
        DrawSetTransform(p+new Vector2(0,59),0,new Vector2(1,.27f));DrawCircle(Vector2.Zero,85,new Color(0,0,0,.23f));DrawSetTransform(Vector2.Zero);
        Color body=new(enemy?s.Enemy.Elite||s.Floor%4==0?"a26882":"987c86":"7cad9d");
        if(!enemy&&s.Shield>0)DrawArc(p,86,0,Mathf.Tau,60,new Color("8cbad6"),3,true);
        DrawCircle(p,65,body);DrawCircle(p+new Vector2(-37,36),25,body);DrawCircle(p+new Vector2(37,36),25,body);
        DrawColoredPolygon(new[]{p+new Vector2(-44,-36),p+new Vector2(-49,-82),p+new Vector2(-18,-55)},new Color(enemy?"d1afb0":"c5d1a6"));
        DrawColoredPolygon(new[]{p+new Vector2(44,-36),p+new Vector2(49,-82),p+new Vector2(18,-55)},new Color(enemy?"d1afb0":"c5d1a6"));
        DrawCircle(p+new Vector2(-21,-13),13,new Color("eee6c7"));DrawCircle(p+new Vector2(21,-13),13,new Color("eee6c7"));
        DrawCircle(p+new Vector2(-18,-11),6,new Color("263b3f"));DrawCircle(p+new Vector2(24,-11),6,new Color("263b3f"));
        DrawArc(p+new Vector2(0,5),27,0,Mathf.Pi,24,new Color("273c3a"),5,true);
        if(!enemy)
        {
            for(int i=0;i<6;i++)
            {
                var o=s.Body[i];if(o==null)continue;var pos=p+new Vector2((i%3-1)*29,35+(i/3)*23);
                DrawCircle(pos,10,new Color(OrganColor(o.Kind)));if(o.Level>=2)DrawCircle(pos,4,new Color("f6e8b6"));
            }
            for(int i=0;i<s.Drones;i++)DrawCircle(p+new Vector2(Mathf.Cos(Clock+i)*105,Mathf.Sin(Clock+i)*45),5,new Color("bfd49b"));
        }
        else if(s.Enemy.Armor>0)DrawArc(p,75,-2.9f,-.25f,30,new Color("aab9cc"),8,true);
    }
    private string OrganColor(string kind)=>SixfoldRun.Catalog.First(c=>c.Id==kind).Color;
}
