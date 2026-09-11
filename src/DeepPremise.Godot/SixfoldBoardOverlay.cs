using Godot;
using DeepPremise.Core.Sixfold;
using System;
using System.Linq;
using System.Collections.Generic;

namespace DeepPremise.Godot;

public partial class SixfoldGlyph : Control
{
    public string Kind="jaw";
    public override void _Draw()=>Paint(this,new Vector2(16,16),Kind,16);
    public static void Paint(CanvasItem canvas,Vector2 p,string kind,float r)
    {
        var color=new Color(SixfoldRun.Catalog.First(c=>c.Id==kind).Color);
        if(kind is "jaw" or "spike")canvas.DrawColoredPolygon(new[]{p+new Vector2(-r,-r*.6f),p+new Vector2(r,0),p+new Vector2(-r,r*.6f)},color);
        else if(kind is "shell" or "lung")canvas.DrawColoredPolygon(new[]{p+new Vector2(-r,-r),p+new Vector2(r,-r),p+new Vector2(r*.8f,r*.4f),p+new Vector2(0,r),p+new Vector2(-r*.8f,r*.4f)},color);
        else if(kind=="stomach"){canvas.DrawLine(p-new Vector2(r,0),p+new Vector2(r,0),color,7);canvas.DrawLine(p-new Vector2(0,r),p+new Vector2(0,r),color,7);}
        else if(kind=="eye"){canvas.DrawArc(p,r,0,Mathf.Tau,24,color,3,true);canvas.DrawCircle(p,r*.4f,color);}
        else if(kind=="heart"){canvas.DrawCircle(p+new Vector2(-r*.4f,-r*.3f),r*.6f,color);canvas.DrawCircle(p+new Vector2(r*.4f,-r*.3f),r*.6f,color);canvas.DrawColoredPolygon(new[]{p+new Vector2(-r,-r*.2f),p+new Vector2(r,-r*.2f),p+new Vector2(0,r)},color);}
        else if(kind=="brood"){for(int i=0;i<3;i++)canvas.DrawCircle(p+new Vector2((i-1)*r*.8f,(i%2)*r*.5f),r*.35f,color);}
        else{canvas.DrawCircle(p+new Vector2(0,r*.3f),r*.7f,color);canvas.DrawColoredPolygon(new[]{p+new Vector2(-r*.65f,r*.1f),p+new Vector2(r*.65f,r*.1f),p-new Vector2(0,r)},color);}
    }
}

public partial class SixfoldBoardOverlay : Control
{
    public Func<SixfoldState> Read=null!;
    public List<Button> Slots=null!;
    public Func<int> Selected=null!;
    public override void _Draw()
    {
        var s=Read();if(Slots.Count!=6)return;
        for(int i=0;i<6;i++)
        {
            var o=s.Body[i];if(o==null)continue;
            var transform=GetGlobalTransformWithCanvas().AffineInverse();
            var rect=new Rect2(transform*Slots[i].GetGlobalTransformWithCanvas().Origin,Slots[i].Size);
            SixfoldGlyph.Paint(this,rect.Position+new Vector2(22,24),o.Kind,11);
            var spec=SixfoldRun.Catalog.First(c=>c.Id==o.Kind);var color=new Color(spec.Color);
            if(spec.Interval>0)
            {
                int hearts=SixfoldRun.Neighbors(i).Sum(n=>s.Body[n]?.Kind=="heart"?s.Body[n]!.Level:0);
                float progress=s.Phase=="battle"?(float)o.Clock/Math.Max(6,spec.Interval-hearts*2-(o.Adapted?3:0)):1;
                var p=rect.Position+new Vector2(12,rect.Size.Y-7);DrawLine(p,p+new Vector2(rect.Size.X-24,0),new Color("46515e"),3);
                DrawLine(p,p+new Vector2((rect.Size.X-24)*progress,0),color,3);
            }
            foreach(int n in SixfoldRun.Neighbors(i))
            {
                var other=s.Body[n];if(other==null)continue;
                bool link=o.Kind is "heart" or "eye" || o.Kind=="shell"&&other.Kind=="spike" || o.Kind=="stomach"&&other.Kind=="venom";
                if(!link)continue;
                var end=transform*(Slots[n].GetGlobalTransformWithCanvas()* (Slots[n].Size/2));var start=rect.GetCenter();var dir=(end-start).Normalized();
                float half=Math.Abs(dir.X)>.5f?rect.Size.X/2:rect.Size.Y/2;
                start+=dir*half;end-=dir*half;DrawLine(start,end,color,Selected()==i||Selected()==n?5:3,true);DrawCircle((start+end)/2,4,color);
            }
        }
    }
}
