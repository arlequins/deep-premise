extends SceneTree
const World = preload("res://game/core/world.gd")
const Session = preload("res://game/core/session.gd")
var failures = 0
func check(ok: bool, message: String) -> void:
	if not ok: failures += 1; push_error(message)
func _initialize() -> void:
	call_deferred("run")
func run() -> void:
	var a = World.new()
	a.start(12345)
	var b = World.new()
	b.start(12345)
	a.advance(2400)
	b.advance(2400)
	check(a.s == b.s, "Deterministic replay")
	var restored = World.new()
	check(restored.load_state(JSON.parse_string(JSON.stringify(a.s, "", true, true))), "Save round trip")
	a.advance(240)
	restored.advance(240)
	check(equivalent(a.s, restored.s), "Replay after serialization (float tolerance 1e-10)")
	var v = a.observe()
	check(not v.has("events") and not v.has("rng"), "Hidden world state")
	check(not v.people[0].has("trust") and not v.people[0].has("ties"), "Hidden person state")
	check(not v.reports[0].has("event_id"), "Hidden report provenance")
	var invalid = a.s.duplicate(true)
	invalid.people[0].partner = 999
	check(not restored.load_state(invalid), "Reject invalid person reference")
	invalid = a.s.duplicate(true)
	invalid.reports = [{"text":"broken"}]
	check(not restored.load_state(invalid), "Reject incomplete report")
	check(a.act({"type":"talk","id":"oops"}) != "", "Reject string id")
	check(a.act({"type":"talk","id":0.5}) != "", "Reject fractional id")
	var baseline = World.new()
	baseline.start(999)
	var repair = World.new()
	repair.start(999)
	repair.act({"type":"policy","value":"repair"})
	baseline.advance(720)
	repair.advance(720)
	check(repair.s.repair > baseline.s.repair, "Repair policy has causal effect")
	var convoy = World.new()
	convoy.start()
	check(convoy.act({"type":"convoy"}) == "", "Start convoy")
	check(convoy.act({"type":"move","id":convoy.s.tactical.units[0].id,"x":"bad","y":1}) != "", "Reject malformed movement")
	check(not convoy.observe().tactical.has("threat"), "Hide tactical threat")
	convoy.advance(72)
	check(convoy.s.tactical.is_empty(), "Autonomous convoy resolution")
	var longrun = World.new()
	longrun.start(777)
	longrun.advance(24000)
	check(longrun.s.events.size()<=2500 and longrun.s.reports.size()<=300, "Bounded long run")
	check(restored.load_state(JSON.parse_string(JSON.stringify(longrun.s, "", true, true))), "Long run save valid")
	print("LONG RUN: days=1000 residents=", longrun.s.people.filter(func(p): return p.alive).size(), " reports=", longrun.s.reports.size())
	var folder = "res://.tools/test-save-" + str(Time.get_ticks_usec())
	var first = Session.new()
	root.add_child(first)
	check(first.open(folder), "Open isolated save")
	first.command({"type":"notes","value":"한글 저장 확인"})
	first.save()
	var second = Session.new()
	root.add_child(second)
	check(second.open(folder) and second.observe().notes == "한글 저장 확인", "Session reload")
	var file = FileAccess.open(second.save_file,FileAccess.WRITE)
	file.store_string("broken")
	file.close()
	var third = Session.new()
	root.add_child(third)
	check(third.open(folder) and third.observe().notes == "한글 저장 확인", "Backup recovery")
	first.queue_free()
	second.queue_free()
	third.queue_free()
	print("SIMULATION TESTS: ", "PASS" if failures == 0 else "FAIL", " (", failures, " failures)")
	quit(failures)

func equivalent(a, b) -> bool:
	if a is Dictionary:
		if not b is Dictionary or a.size()!=b.size(): return false
		for key in a:
			if not b.has(key) or not equivalent(a[key],b[key]): return false
		return true
	if a is Array:
		if not b is Array or a.size()!=b.size(): return false
		for i in range(a.size()):
			if not equivalent(a[i],b[i]): return false
		return true
	if a is float or a is int: return abs(float(a)-float(b))<0.0000000001
	return a == b
