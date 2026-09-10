using Godot;
using DeepPremise.Core.Colony;
using DeepPremise.Core.Localization;
using System;
using System.Linq;

namespace DeepPremise.Godot;

public partial class ColonyMap : Control
{
    private ColonyView? world;
    private TextCatalog catalog = new("en");
    public Action<int,int>? CellClicked;
    public Action? CancelPlacement;
    public string Placing = "";
    public Func<string,int,int,bool>? CanBuild;
    public int SelectedPawn, SelectedBuilding;
    private Vector2I hover = new(-1,-1);
    private float cell;
    private Vector2 origin;
    private double time;
    public void Present(ColonyView view,TextCatalog text) {world=view;catalog=text;QueueRedraw();}
    public override void _Ready() {MouseFilter=MouseFilterEnum.Stop;ClipContents=true;}
    public override void _Process(double delta){time+=delta;QueueRedraw();}
    private Vector2 Point(int x,int y)=>origin+new Vector2((x+.5f)*cell,(y+.5f)*cell);
    private void Text(string value,Vector2 at,int size,Color color) => DrawString(GetThemeDefaultFont(),at,catalog.Text(value),HorizontalAlignment.Left,-1,size,color);
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseMotion motion){hover=new((int)Math.Floor((motion.Position.X-origin.X)/cell),(int)Math.Floor((motion.Position.Y-origin.Y)/cell));}
        if(e is InputEventMouseButton {Pressed:true} click)
        {
            if(click.ButtonIndex==MouseButton.Right){CancelPlacement?.Invoke();AcceptEvent();return;}
            if(click.ButtonIndex==MouseButton.Left){var x=(int)Math.Floor((click.Position.X-origin.X)/cell);var y=(int)Math.Floor((click.Position.Y-origin.Y)/cell);if(x>=0&&y>=0&&x<ColonySimulation.Width&&y<ColonySimulation.Height)CellClicked?.Invoke(x,y);AcceptEvent();}
        }
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("182822"));if(world==null)return;
        cell=Math.Min(Size.X/ColonySimulation.Width,(Size.Y-28)/ColonySimulation.Height);origin=(Size-new Vector2(cell*ColonySimulation.Width,cell*ColonySimulation.Height))/2;
        var night=(8+world.Tick/10)%24 is >=21 or <6;
        for(var y=0;y<ColonySimulation.Height;y++)for(var x=0;x<ColonySimulation.Width;x++)
        {
            var terrain=world.Terrain[y*ColonySimulation.Width+x];var shade=((x*19+y*31)%7)*.008f;
            var color=terrain=="water"?new Color(.18f+shade,.34f+shade,.37f+shade):terrain=="earth"?new Color(.35f+shade,.36f+shade,.24f+shade):new Color(.28f+shade,.37f+shade,.25f+shade);
            if(night)color=color.Darkened(.22f);DrawRect(new Rect2(origin+new Vector2(x*cell,y*cell),new Vector2(cell+1,cell+1)),color);
            if(terrain=="water"){var p=Point(x,y);DrawLine(p+new Vector2(-cell*.25f,(float)Math.Sin(time+x)*2),p+new Vector2(cell*.2f,(float)Math.Sin(time+x)*2),new Color(.5f,.7f,.7f,.22f),1);}
            else if((x+y*3)%9==0)DrawLine(Point(x,y)+new Vector2(-3,4),Point(x,y)+new Vector2(0,-2),new Color(.5f,.58f,.33f,.5f),1);
        }
        // Footpaths keep the first camp legible within the larger clearing.
        DrawLine(Point(5,10),Point(22,10),new Color(.57f,.5f,.33f,.22f),cell*.55f);
        foreach(var r in world.Resources.Where(r=>r.Amount>0))
        {
            var p=Point(r.X,r.Y);DrawCircle(p+new Vector2(2,4),cell*.3f,new Color(0,0,0,.15f));
            if(r.Kind=="wood")
            {DrawRect(new Rect2(p+new Vector2(-2,0),new Vector2(4,cell*.35f)),new Color("796143"));DrawCircle(p+new Vector2(0,-cell*.15f),cell*.31f,new Color("294a35"));DrawCircle(p+new Vector2(-cell*.08f,-cell*.22f),cell*.23f,new Color("466342"));}
            else if(r.Kind=="stone")
            {DrawColoredPolygon(new[]{p+new Vector2(-cell*.3f,cell*.17f),p+new Vector2(-cell*.22f,-cell*.12f),p+new Vector2(cell*.1f,-cell*.25f),p+new Vector2(cell*.33f,.1f*cell)},new Color("929487"));}
            else {DrawCircle(p,cell*.2f,new Color("596c37"));DrawCircle(p+new Vector2(3,-2),3,new Color("cba258"));DrawCircle(p+new Vector2(-3,3),2,new Color("bd805d"));}
            if(r.Designated){DrawArc(p,cell*.4f,0,Mathf.Tau,20,new Color("f3d181"),2);DrawLine(p+new Vector2(0,-cell*.4f),p+new Vector2(0,-cell*.65f),new Color("f3d181"),2);}
        }
        foreach(var b in world.Buildings.OrderBy(b=>b.Y))
        {
            var d=ColonySimulation.Definition(b.Kind);var rect=new Rect2(origin+new Vector2(b.X*cell,b.Y*cell),new Vector2(d.Width*cell,d.Height*cell));
            DrawRect(new Rect2(rect.Position+new Vector2(4,5),rect.Size),new Color(0,0,0,.18f));
            if(!b.Built)
            {
                DrawRect(rect,new Color(.65f,.75f,.65f,.14f));DrawRect(rect.Grow(-2),new Color("bac8a3"),false,1);
                DrawLine(rect.Position,rect.End,new Color(.8f,.9f,.7f,.25f),1);DrawLine(new Vector2(rect.End.X,rect.Position.Y),new Vector2(rect.Position.X,rect.End.Y),new Color(.8f,.9f,.7f,.25f),1);
                Bar(rect.Position+new Vector2(3,rect.Size.Y-6),rect.Size.X-6,(float)b.Work/b.Required,new Color("d7c081"));
            }
            else if(b.Kind=="camp")
            {DrawCircle(rect.GetCenter(),cell*.4f,new Color("6e6b58"));DrawCircle(rect.GetCenter(),cell*.23f,new Color("d8924f"));DrawCircle(rect.GetCenter()+new Vector2(0,-2),cell*(.12f+(float)Math.Sin(time*6)*.025f),new Color("ffdc82"));}
            else if(b.Kind=="field")
            {
                DrawRect(rect.Grow(-2),new Color("625338"));for(var row=0;row<4;row++){var yy=rect.Position.Y+cell*.25f+row*cell*.4f;DrawLine(new(rect.Position.X+5,yy),new(rect.End.X-5,yy),new Color("3b4029"),3);for(var col=0;col<4;col++)DrawCircle(new(rect.Position.X+cell*.3f+col*cell*.43f,yy-2),2+b.Cycle*.045f,new Color("8da65a"));}
            }
            else if(b.Kind is "shelter" or "storehouse")
            {
                DrawRect(rect.Grow(-4),new Color("9c8860"));var roof=b.Kind=="shelter"?new Color("a78155"):new Color("76836d");DrawColoredPolygon(new[]{rect.Position+new Vector2(-2,cell*.65f),rect.Position+new Vector2(rect.Size.X/2,-4),rect.Position+new Vector2(rect.Size.X+2,cell*.65f),rect.Position+new Vector2(rect.Size.X-3,cell*.9f),rect.Position+new Vector2(3,cell*.9f)},roof);
                DrawRect(new Rect2(rect.Position+new Vector2(cell*.8f,cell*1.2f),new Vector2(cell*.38f,cell*.65f)),new Color("3b3d2c"));
            }
            else
            {
                DrawRect(rect.Grow(-3),new Color(b.Kind=="quarry"?"747d70":"786648"));DrawRect(new Rect2(rect.Position+new Vector2(7,cell*.35f),new Vector2(rect.Size.X-14,cell*.55f)),new Color("b09769"));
                DrawLine(rect.Position+new Vector2(cell*.5f,cell*.3f),rect.Position+new Vector2(cell*1.3f,cell*.8f),new Color("d7dac0"),3);
            }
            if(b.Built&&b.Kind is "field" or "workbench" or "woodcamp" or "quarry")Bar(rect.Position+new Vector2(3,rect.Size.Y-5),rect.Size.X-6,b.Cycle/(b.Kind=="field"?36f:22f),new Color("a5bb77"));
            if(SelectedBuilding==b.Id)DrawRect(rect.Grow(2),new Color("ffe3a0"),false,2);
            if(b.Kind!="camp")Text(d.Name,rect.Position+new Vector2(0,-6),Math.Max(10,(int)(cell*.36f)),new Color("f3ecd4"));
        }
        foreach(var p in world.Pawns.OrderBy(p=>p.Y))
        {
            var point=Point(p.X,p.Y)+new Vector2((p.Id%3-1)*5,0);var body=p.Name switch{"Mara"=>new Color("d59b69"),"Orren"=>new Color("8dacc0"),"Neri"=>new Color("b0a1c1"),_=>new Color("a7bd7d")};
            if(SelectedPawn==p.Id){DrawArc(point,cell*.45f,0,Mathf.Tau,24,new Color("ffe3a0"),2);DrawLine(point,Point(p.TargetX,p.TargetY),new Color(1,.9f,.65f,.35f),1);}
            DrawCircle(point+new Vector2(2,5),cell*.2f,new Color(0,0,0,.25f));DrawRect(new Rect2(point+new Vector2(-5,-1),new Vector2(10,12)),body);DrawCircle(point+new Vector2(0,-4),5,new Color("debf94"));
            var label=new Rect2(point+new Vector2(-28,-33),new Vector2(64,17));DrawRect(label,new Color(.06f,.1f,.08f,.78f));Text(p.Name,label.Position+new Vector2(4,13),12,new Color("fff4d5"));
            Text(p.Job,point+new Vector2(-25,27),10,new Color("f4ecd6"));
            if(p.Required>0&&p.Job!="rest")Bar(point+new Vector2(-12,13),24,(float)p.Progress/p.Required,body);
        }
        if(Placing!=""&&hover.X>=0&&hover.Y>=0&&hover.X<ColonySimulation.Width&&hover.Y<ColonySimulation.Height)
        {
            var d=ColonySimulation.Definition(Placing);var valid=CanBuild?.Invoke(Placing,hover.X,hover.Y)==true;var rect=new Rect2(origin+new Vector2(hover.X*cell,hover.Y*cell),new Vector2(d.Width*cell,d.Height*cell));
            DrawRect(rect,new Color(valid ? .7f : 1,valid ? .85f : .4f,.4f,.3f));DrawRect(rect,new Color(valid?"d0efac":"efaa91"),false,2);
        }
    }
    private void Bar(Vector2 at,float width,float fraction,Color color){DrawRect(new Rect2(at,new Vector2(width,3)),new Color(.08f,.12f,.09f,.8f));DrawRect(new Rect2(at,new Vector2(width*Math.Clamp(fraction,0,1),3)),color);}
}
