extends Control

const Session = preload("res://game/core/session.gd")
const Map = preload("res://game/ui/city_map.gd")
var session = Session.new()
var city = Map.new()
var view: Dictionary = {}
var heading: Label
var resources: Label
var status: Label
var detail: VBoxContainer
var letters: VBoxContainer
var tactics: VBoxContainer
var notes: TextEdit
var selected_place = "hall"
var selected_unit = -1
var selected_person = -1
var residents: VBoxContainer
var report_limit = 40
var tabs: TabContainer

func label(text: String, parent: Node, font_size: int = 16) -> Label:
	var node = Label.new()
	node.text = text
	node.add_theme_font_size_override("font_size", font_size)
	node.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(node)
	return node

func button(text: String, parent: Node, callback: Callable) -> Button:
	var node = Button.new()
	node.text = text
	node.custom_minimum_size.y = 38
	node.pressed.connect(callback)
	parent.add_child(node)
	return node

func box(parent: Node) -> VBoxContainer:
	var node = VBoxContainer.new()
	node.add_theme_constant_override("separation", 12)
	parent.add_child(node)
	return node

func page(title: String) -> VBoxContainer:
	var scroll = ScrollContainer.new()
	scroll.name = title
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	tabs.add_child(scroll)
	var content = box(scroll)
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return content

func _ready() -> void:
	var skin = Theme.new()
	var readable_font = FontVariation.new()
	readable_font.base_font = load("res://game/assets/NotoSansKR.ttf")
	readable_font.variation_opentype = {"wght":450}
	skin.default_font = readable_font
	skin.default_font_size = 16
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		var style = StyleBoxFlat.new()
		style.bg_color = Color("344c43") if state == "hover" else Color("213831")
		style.border_color = Color("a69464") if state == "pressed" else Color("496051")
		style.set_border_width_all(1)
		style.set_corner_radius_all(5)
		style.content_margin_left = 12
		style.content_margin_right = 12
		skin.set_stylebox(state, "Button", style)
	theme = skin
	var margin = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + side, 20)
	add_child(margin)
	var root = box(margin)
	heading = label("UNSEEN ORDER  /  여울의 회관", root, 27)
	var bar = HBoxContainer.new()
	root.add_child(bar)
	resources = label("", bar)
	resources.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for entry in [["Ⅱ 멈춤", 0], ["▶ 1배", 1], ["4배", 4], ["12배", 12]]:
		button(entry[0], bar, func(): command({"type":"speed", "value":entry[1]}))
	var body = HSplitContainer.new()
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.split_offset = 790
	root.add_child(body)
	var left = box(body)
	left.custom_minimum_size.x = 500
	city.custom_minimum_size = Vector2(500, 430)
	city.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left.add_child(city)
	city.place_selected.connect(func(id): selected_place = id; selected_person = -1; refresh_place())
	label("장소를 눌러 이웃을 만나세요. 세계는 자리를 비운 동안에도 흐릅니다.", left, 14)
	detail = box(left)
	detail.custom_minimum_size.y = 140
	tabs = TabContainer.new()
	tabs.custom_minimum_size.x = 470
	tabs.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_child(tabs)
	letters = page("편지함")
	var office = page("회관")
	label("오늘의 일은 맡겨 두세요", office, 24)
	label("자리를 비운 동안에도 아래 방침에 따라 회관이 움직입니다.", office)
	for entry in [["기존 배분 유지", "balanced"], ["배식 문턱 낮추기", "relief"], ["수로 보수 · 하루 3닢", "repair"]]:
		button(entry[0], office, func(): command({"type":"policy", "value":entry[1]}))
	label("호송 경로", office, 20)
	button("물길 이용", office, func(): command({"type":"route", "value":"river"}))
	button("육로 이용", office, func(): command({"type":"route", "value":"road"}))
	button("공개 회합 준비 · 8닢", office, func(): command({"type":"petition"}))
	button("식량 호송 준비 · 12닢", office, func(): command({"type":"convoy"}); tabs.current_tab = 2)
	tactics = page("호송")
	var notebook = page("수첩")
	label("나만의 기록", notebook, 24)
	label("보고와 직접 본 일을 연결해 보세요. 메모는 자동으로 저장됩니다.", notebook)
	notes = TextEdit.new()
	notes.custom_minimum_size.y = 420
	notebook.add_child(notes)
	var save_timer = Timer.new()
	save_timer.one_shot = true
	save_timer.wait_time = 0.8
	add_child(save_timer)
	notes.text_changed.connect(func(): save_timer.start())
	save_timer.timeout.connect(func(): command({"type":"notes", "value":notes.text}))
	residents = page("이웃")
	status = label("Space 일시정지  ·  Ctrl+S 저장", root, 14)
	add_child(session)
	DisplayServer.window_set_min_size(Vector2i(1100, 760))
	var directory = ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--save-dir="): directory = arg.trim_prefix("--save-dir=")
	if not session.open(directory):
		status.text = session.save_error
		return
	session.updated.connect(refresh)
	notes.text = session.observe().notes
	refresh()
	get_tree().auto_accept_quit = false

func command(action: Dictionary) -> void:
	var result = session.command(action)
	status.text = result.get("error", "기록했습니다.  ·  Space 일시정지  ·  Ctrl+S 저장")

func clear(parent: Node) -> void:
	for child in parent.get_children():
		parent.remove_child(child)
		child.queue_free()

func refresh() -> void:
	view = session.observe()
	resources.text = "%d일  %02d:00  ·  은화 %d닢  ·  회관 식량 %d몫  ·  %s" % [view.day, view.hour, view.treasury, view.stores, "멈춤" if view.speed == 0 else "%d배" % view.speed]
	heading.text = "UNSEEN ORDER  /  여울의 회관     ·     " + {"balanced":"기존 배분", "relief":"배식 확대", "repair":"수로 보수"}[view.policy]
	city.update_view(view)
	refresh_place()
	refresh_letters()
	refresh_tactics()
	refresh_residents()
	if view.save_error != "": status.text = view.save_error

func refresh_place() -> void:
	if view.is_empty(): return
	if selected_person >= 0:
		show_person(view.people[selected_person])
		return
	clear(detail)
	var place = view.places.filter(func(p): return p.id == selected_place)[0]
	label(place.name, detail, 21)
	var locals = view.people.filter(func(p): return p.location == selected_place and p.alive)
	var row = HBoxContainer.new()
	detail.add_child(row)
	for p in locals.slice(0, 3):
		button(p.name + " · " + p.job, row, func(): show_person(p))
	label("지금 보이는 이웃 %d명  ·  회합 대표 %s" % [locals.size(), view.representative], detail, 14)

func show_person(person: Dictionary) -> void:
	selected_person = int(person.id)
	clear(detail)
	label(person.name + "  /  " + person.species + " · " + person.job, detail, 21)
	label(person.appearance, detail)
	var row = HBoxContainer.new()
	detail.add_child(row)
	button("안부 청하기", row, func(): command({"type":"talk", "id":person.id}))
	button("관심 기록 해제" if person.id in view.tracked else "관심 인물로 기록", row, func(): command({"type":"track", "id":person.id}))

func refresh_letters() -> void:
	clear(letters)
	label("창가의 편지함", letters, 25)
	label("읽지 않은 편지 %d통  ·  조사 회신 대기 %d건" % [view.unread, view.investigations], letters)
	for r in view.reports.slice(0, report_limit):
		var card = PanelContainer.new()
		letters.add_child(card)
		var content = box(card)
		label(("● " if not r.read else "") + r.source, content, 18)
		label("%d일 %02d시 작성 → %d일 도착" % [int(r.written)/24+1, int(r.written)%24, int(r.arrives)/24+1], content, 12)
		label(r.text, content)
		var actions = HBoxContainer.new()
		content.add_child(actions)
		if not r.read: button("읽음", actions, func(): command({"type":"read", "id":r.id}))
		if r.can_investigate: button("더 알아보기 · 3닢", actions, func(): command({"type":"investigate", "id":r.id}))
		if r.subject >= 0: button("인물 보기", actions, func(): show_person(view.people[int(r.subject)]))

	if view.reports.size() > report_limit:
		button("이전 편지 더 보기", letters, func(): report_limit += 40; refresh_letters())

func refresh_residents() -> void:
	clear(residents)
	label("이웃의 이름들", residents, 24)
	label("관심 인물", residents, 20)
	for id in view.tracked:
		var p = view.people[int(id)]
		button(p.name + " · " + p.appearance, residents, func(): show_person(p))
	label("회관에 등록된 이웃", residents, 20)
	for p in view.people:
		button(p.name + " · " + p.job, residents, func(): show_person(p))

func refresh_tactics() -> void:
	clear(tactics)
	label("북문 밖의 호송", tactics, 24)
	var t = view.tactical
	if t.is_empty():
		label("진행 중인 호송이 없습니다. 회관에서 식량 호송을 준비할 수 있습니다.", tactics)
		return
	label("%d번째 차례  ·  수레 상태 %d / 5\n%s" % [t.round, t.wagon.hp, t.log], tactics)
	label("대원 선택 → 인접한 칸 이동 / 두 칸 안 상대 공격\n직접 지휘하지 않으면 현장 판단으로 진행합니다.", tactics, 14)
	for u in t.units:
		var choice = button("%s  ·  체력 %d  ·  행동 %d%s" % [u.name, u.hp, u.ap, "  ◀" if selected_unit == u.id else ""], tactics, func(): selected_unit = int(u.id); refresh_tactics())
		choice.disabled = u.hp <= 0
	var grid = GridContainer.new()
	grid.columns = 7
	tactics.add_child(grid)
	for y in range(5):
		for x in range(7):
			var text = "·"
			var enemy_id = ""
			if t.cover.any(func(c): return c.x == x and c.y == y): text = "▧"
			var wagon_here = t.wagon.x == x and t.wagon.y == y
			if wagon_here: text = "수레"
			for u in t.units:
				if u.hp > 0 and u.x == x and u.y == y: text = "호위+" if wagon_here else "호위"
			for e in t.enemies:
				if e.hp > 0 and e.x == x and e.y == y: text = "상대"; enemy_id = e.id
			var cell = button(text, grid, func():
				if enemy_id != "": command({"type":"attack", "id":selected_unit, "target":enemy_id})
				else: command({"type":"move", "id":selected_unit, "x":x, "y":y}))
			cell.custom_minimum_size = Vector2(56, 48)
	button("차례 마치기", tactics, func(): command({"type":"turn"}))
	button("사람들을 데리고 철수", tactics, func(): command({"type":"retreat"}))

func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE and not notes.has_focus():
			command({"type":"speed", "value":1 if session.speed == 0 else 0})
		if event.keycode == KEY_S and event.ctrl_pressed:
			command({"type":"notes", "value":notes.text})

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		if session.running:
			session.command({"type":"notes", "value":notes.text})
		get_tree().quit()
