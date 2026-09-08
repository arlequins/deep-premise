class_name CityMap
extends Control

signal place_selected(id: String)
var view: Dictionary={}
var selected="hall"
var people_positions={}
var font: Font
var phase=0.0
var hovered=""
var last_tick=-1
const LAND=Color("243b33")
const GOLD=Color("cbb77f")

func _ready() -> void:
	mouse_default_cursor_shape=Control.CURSOR_POINTING_HAND
	clip_contents=true
	font=load("res://game/assets/NotoSansKR.ttf")

func location(p: Dictionary) -> Vector2:
	return Vector2(p.x,p.y)*size

func update_view(v: Dictionary) -> void:
	view=v
	queue_redraw()

func _process(delta: float) -> void:
	phase+=delta
	if view.is_empty(): return
	for p in view.people:
		var where=view.places.filter(func(l): return l.id==p.location)[0]
		var target=location(where)+Vector2(sin(p.id*2.7+int(view.tick)*0.2)*33,cos(p.id*1.8+int(view.tick)*0.16)*25)
		var key=int(p.id)
		if not people_positions.has(key): people_positions[key]=target
		people_positions[key]=people_positions[key].lerp(target,minf(1,delta*0.6))
	queue_redraw()

func _draw() -> void:
	if view.is_empty() or font==null: return
	draw_rect(Rect2(Vector2.ZERO,size),Color("172b2b"))
	var river=PackedVector2Array()
	for i in range(61):
		var y=float(i)/60*size.y
		var x=size.x*(0.77+sin(float(i)/60*8)*0.065)
		river.append(Vector2(x,y))
	draw_polyline(river,Color("344f49"),size.x*0.13,true)
	draw_polyline(river,Color("355c60"),size.x*0.09,true)
	for i in range(14):
		var y=fmod(i*61+phase*3,size.y)
		var x=size.x*(0.77+sin(y/size.y*8)*0.065)
		draw_line(Vector2(x-8,y),Vector2(x+11,y-3),Color(0.65,0.79,0.74,0.15),1,true)
	for i in range(74):
		var pos=Vector2(20+(i*137)%maxi(1,int(size.x)-40),25+(i*79)%maxi(1,int(size.y)-50))
		if pos.x>size.x*0.67: continue
		draw_circle(pos+Vector2(3,8),10,Color(0,0,0,0.12))
		draw_line(pos,pos+Vector2(0,13),Color("516754"),2)
		draw_circle(pos,5+i%5,Color("3c5545"))
		draw_circle(pos-Vector2(2,3),3+i%3,Color("506448"))
	var center=location(view.places[0])
	for p in view.places:
		var end=location(p)
		var road=PackedVector2Array()
		for i in range(16):
			var t=float(i)/15
			road.append(center.lerp(end,t)+Vector2(sin(t*PI)*18,sin(t*PI)*-12))
		draw_polyline(road,Color("58644c"),9,true)
		draw_polyline(road,Color("899170"),1,true)
	var farm=location(view.places[2])
	for i in range(6): draw_line(farm+Vector2(-48,-16+i*9),farm+Vector2(45,8+i*9),Color("7d8450"),4,true)
	for i in range(40):
		var p=view.places[i%8]
		var pos=location(p)+Vector2((i*43)%82-41,(i*29)%68-34)
		building(pos,0.6,Color("526a5a"))
	for p in view.people:
		if not p.alive: continue
		var pos=people_positions.get(int(p.id),Vector2.ZERO)
		draw_circle(pos+Vector2(1,3),3,Color(0,0,0,0.25))
		draw_line(pos,pos+Vector2(0,4),Color("c4b77e") if p.species=="이어인" else Color("aec0aa"),2)
		draw_circle(pos-Vector2(0,1),1.7,Color("d7cead"))
	for p in view.places:
		var pos=location(p)
		var active=p.id==selected or p.id==hovered
		draw_circle(pos+Vector2(3,10),26,Color(0,0,0,0.15))
		draw_arc(pos,28,0,TAU,48,GOLD if active else Color("7b8b6555"),2 if active else 1,true)
		building(pos,1.3,Color("7f8b6c"))
		var width=font.get_string_size(p.name,HORIZONTAL_ALIGNMENT_LEFT,-1,13).x
		draw_rect(Rect2(pos+Vector2(-width/2-5,33),Vector2(width+10,22)),Color("162b28cc"))
		draw_string(font,pos+Vector2(-width/2,49),p.name,HORIZONTAL_ALIGNMENT_LEFT,-1,13,Color("e0ddbf"))
	var north=Vector2(size.x-33,34)
	draw_line(north+Vector2(0,8),north+Vector2(0,33),Color("a6b09688"),1)
	draw_string(font,north+Vector2(-5,0),"N",HORIZONTAL_ALIGNMENT_LEFT,-1,11,Color("a6b096"))
	if view.hour<6 or view.hour>=21:
		draw_rect(Rect2(Vector2.ZERO,size),Color(0.02,0.03,0.12,0.30))
		for p in view.places: draw_circle(location(p)+Vector2(4,6),3,Color("e6bc6a"))

func building(pos: Vector2,scale_value: float, roof: Color) -> void:
	var a=pos+Vector2(-13,-5)*scale_value
	var b=pos+Vector2(2,3)*scale_value
	var c=pos+Vector2(19,-7)*scale_value
	var d=pos+Vector2(3,-15)*scale_value
	draw_colored_polygon(PackedVector2Array([a,b,b+Vector2(0,15)*scale_value,a+Vector2(0,14)*scale_value]),Color("344f44"))
	draw_colored_polygon(PackedVector2Array([b,c,c+Vector2(0,15)*scale_value,b+Vector2(0,15)*scale_value]),Color("506b57"))
	draw_colored_polygon(PackedVector2Array([a,b,c,d]),roof)
	draw_line(a,b,Color("b8b89588"),1,true)
	draw_line(b,c,Color("b8b89588"),1,true)
	draw_line(b+Vector2(5,4)*scale_value,b+Vector2(5,10)*scale_value,Color("c4b881aa"),2)

func nearest(pos: Vector2) -> String:
	for p in view.get("places",[]):
		if location(p).distance_to(pos)<45: return p.id
	return ""

func _gui_input(e: InputEvent) -> void:
	if e is InputEventMouseMotion: hovered=nearest(e.position)
	if e is InputEventMouseButton and e.pressed and e.button_index==MOUSE_BUTTON_LEFT:
		var id=nearest(e.position)
		if id!="":
			selected=id
			place_selected.emit(id)
