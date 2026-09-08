extends SceneTree
func _initialize() -> void:
	call_deferred("run")
func run() -> void:
	var scene = load("res://game/main.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	scene.session.command({"type":"speed","value":0})
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://artifacts/game-desktop.png")
	scene.session.command({"type":"convoy"})
	scene.tabs.current_tab = 2
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://artifacts/game-convoy.png")
	scene.session.command({"type":"retreat"})
	scene.session.command({"type":"notes","value":"화면 검사"})
	print("NATIVE UI SMOKE PASS")
	quit()
