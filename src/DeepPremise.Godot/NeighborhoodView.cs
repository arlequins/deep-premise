using Godot;
using DeepPremise.Core;
using DeepPremise.Core.Localization;
using System;
using System.Linq;

namespace DeepPremise.Godot;

public partial class NeighborhoodView : Control
{
    private WorldView? view;
    private TextCatalog catalog = new("en");
    private double phase;
    public Action<string>? PlaceSelected;
    public void Present(WorldView value, TextCatalog language) { view = value; catalog = language; QueueRedraw(); }
    public override void _Ready() { MouseDefaultCursorShape = CursorShape.PointingHand; ClipContents = true; }
    public override void _Process(double delta) { phase += delta; QueueRedraw(); }
    public override void _GuiInput(InputEvent @event)
    {
        if (view == null || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        var place = view.Places.OrderBy(p => PositionOf(p).DistanceTo(click.Position)).First();
        if (PositionOf(place).DistanceTo(click.Position) < 55) PlaceSelected?.Invoke(place.Id);
    }
    private Vector2 PositionOf(PlaceView p) => new(p.X * Size.X, p.Y * Size.Y);
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("182d2d"));
        if (view == null) return;
        var river = new Vector2[40];
        for (var i = 0; i < river.Length; i++) river[i] = new Vector2(Size.X * (.91f + MathF.Sin(i * .13f) * .06f), i / 39f * Size.Y);
        DrawPolyline(river, new Color("36504e"), Size.X * .17f, true);
        DrawPolyline(river, new Color("45645f"), 1, true);
        for (var i = 0; i < 8; i++)
        {
            var y = (float)((i * 57 + phase * 5) % Math.Max(1, Size.Y));
            DrawLine(new Vector2(Size.X * .90f, y), new Vector2(Size.X * .94f, y - 4), new Color("7c9b8850"), 1, true);
        }
        var center = PositionOf(view.Places[0]);
        foreach (var place in view.Places)
        {
            var pos = PositionOf(place);
            var road = new Vector2[20];
            for (var i = 0; i < road.Length; i++)
            {
                var t = i / 19f;
                road[i] = center.Lerp(pos, t) + new Vector2(MathF.Sin(t * MathF.PI) * 16, 0);
            }
            DrawPolyline(road, new Color("697365"), 11, true);
            DrawPolyline(road, new Color("89917b"), 1, true);
        }
        for (var i = 0; i < 38; i++)
        {
            var pos = new Vector2((i * 71 % 367) / 400f * Size.X + 10, (i * 43 % 351) / 400f * Size.Y + 20);
            if (view.Places.Any(p => PositionOf(p).DistanceTo(pos) < 58) || pos.X > Size.X * .81f) continue;
            DrawCircle(pos + new Vector2(3, 5), 10, new Color("102621"));
            DrawLine(pos, pos + new Vector2(0, 16), new Color("71816b"), 2, true);
            DrawCircle(pos, 9, new Color("3d5848"));
            DrawCircle(pos - new Vector2(3, 2), 5, new Color("587157"));
        }
        foreach (var p in view.Places)
        {
            var pos = PositionOf(p);
            if (p.Id == view.Place)
            {
                DrawCircle(pos, 39, new Color("cbbf8b15"));
                DrawArc(pos, 39, 0, Mathf.Tau, 48, new Color("d7bb7f"), 1.5f, true);
            }
            DrawCircle(pos + new Vector2(5, 16), 29, new Color("102723"));
            if (p.Id == "courtyard")
            {
                DrawRect(new Rect2(pos - new Vector2(23, 11), new Vector2(46, 22)), new Color("ad9b6e"));
                for (var i = 0; i < 3; i++)
                {
                    DrawRect(new Rect2(pos + new Vector2(-19 + i * 15, -20), new Vector2(10, 6)), new Color("75816a"));
                    DrawRect(new Rect2(pos + new Vector2(-19 + i * 15, 15), new Vector2(10, 6)), new Color("75816a"));
                    DrawCircle(pos + new Vector2(-15 + i * 14, -3), 2, new Color("eee3b7"));
                }
            }
            else
            {
                DrawColoredPolygon(new[] { pos + new Vector2(-26, -3), pos + new Vector2(0, 10), pos + new Vector2(0, 31), pos + new Vector2(-26, 16) }, new Color("425b4d"));
                DrawColoredPolygon(new[] { pos + new Vector2(0, 10), pos + new Vector2(28, -5), pos + new Vector2(28, 15), pos + new Vector2(0, 31) }, new Color("627562"));
                DrawColoredPolygon(new[] { pos + new Vector2(-29, -4), pos + new Vector2(-2, -19), pos + new Vector2(31, -6), pos + new Vector2(0, 12) }, new Color("8e9573"));
                DrawLine(pos + new Vector2(8, 13), pos + new Vector2(8, 23), new Color("dbc28a"), 4);
            }
            var label = p.Id switch { "courtyard" => "COMMON TABLE", "bakery" => "THE OVEN", "workshop" => "MENDING ROOM", _ => "REED LANDING" };
            label = catalog.Text(label);
            var font = GetThemeDefaultFont();
            var width = font.GetStringSize(label, fontSize: 11).X;
            DrawString(font, pos + new Vector2(-width / 2, 52), label, fontSize: 11, modulate: new Color("c8c7a9"));
            var people = view.Residents.Where(a => a.Place == p.Id).ToArray();
            for (var i = 0; i < people.Length; i++)
            {
                var dot = pos + new Vector2(-24 + i * 13, 37);
                DrawCircle(dot, 3, new Color("d6ba80"));
            }
        }
        DrawString(GetThemeDefaultFont(), new Vector2(16, 24), catalog.Text("THE REED QUARTER"), fontSize: 11, modulate: new Color("b8bea1"));
    }
}
