class_name GameSession
extends Node

signal updated
const World = preload("res://game/core/world.gd")
var world=World.new()
var speed=1
var elapsed=0.0
var save_error=""
var folder=""
var save_file=""
var running=false
var revision=0

func open(directory: String = "") -> bool:
	folder=directory if directory!="" else OS.get_user_data_dir()
	DirAccess.make_dir_recursive_absolute(folder)
	save_file=folder.path_join("world-v2.json")
	if FileAccess.file_exists(save_file):
		if not world.load_state(JSON.parse_string(FileAccess.get_file_as_string(save_file))):
			if not world.load_state(JSON.parse_string(FileAccess.get_file_as_string(save_file+".bak"))):
				save_error="저장 파일을 읽지 못했습니다. 원본을 보존했습니다. 저장 폴더를 확인해 주세요."
				return false
			# Keep the damaged primary for diagnosis before restoring its backup.
			DirAccess.copy_absolute(save_file,save_file+".damaged-%d"%int(Time.get_unix_time_from_system()))
			DirAccess.copy_absolute(save_file+".bak",save_file)
	else:
		world.start()
	running=true
	save()
	return true

func _process(delta: float) -> void:
	if not running or speed==0: return
	elapsed+=delta
	# Never simulate hours of OS sleep on resume. Window minimization is not sleep.
	if elapsed>6: elapsed=3
	if elapsed>=3:
		elapsed-=3
		world.advance(speed)
		revision+=1
		if int(world.s.tick)%8<speed: save()
		updated.emit()

func observe() -> Dictionary:
	var v=world.observe()
	v.speed=speed
	v.save_error=save_error
	v.revision=revision
	return v

func command(action: Dictionary) -> Dictionary:
	if not running: return {"error":"세계가 아직 열리지 않았습니다."}
	if action.get("type")=="speed":
		if action.get("value") not in [0,1,4,12]: return {"error":"지원하지 않는 속도입니다."}
		speed=int(action.value)
	else:
		var error=world.act(action)
		if error!="": return {"error":error}
	revision+=1
	save()
	updated.emit()
	return {"ok":true}

func save() -> bool:
	if world.s.is_empty(): return false
	var temp=save_file+".tmp"
	var file=FileAccess.open(temp,FileAccess.WRITE)
	if file==null:
		save_error="저장하지 못했습니다. 저장 공간과 폴더 권한을 확인해 주세요."
		return false
	file.store_string(JSON.stringify(world.s))
	file.flush()
	var error=file.get_error()
	file.close()
	if error!=OK:
		save_error="저장 중 오류가 생겼습니다. 이전 저장은 그대로 남아 있습니다."
		return false
	if FileAccess.file_exists(save_file):
		if DirAccess.copy_absolute(save_file,save_file+".bak")!=OK:
			save_error="예비 저장을 만들지 못했습니다. 이전 저장을 보존했습니다."
			return false
	if DirAccess.rename_absolute(temp,save_file)!=OK:
		save_error="저장 파일을 교체하지 못했습니다. 예비 저장을 보존했습니다."
		return false
	save_error=""
	return true
