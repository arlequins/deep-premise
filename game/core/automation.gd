class_name LocalAutomation
extends Node

var session: GameSession
var server=TCPServer.new()
var clients=[]
var token=""
var connection_file=""

func start(game: GameSession) -> bool:
	session=game
	# Opt-in; no listening port unless the player explicitly enables AI control.
	for port in range(43871,43891):
		if server.listen(port,"127.0.0.1")==OK:
			token=Crypto.new().generate_random_bytes(32).hex_encode()
			connection_file=session.folder.path_join("mcp-connection.json")
			var file=FileAccess.open(connection_file,FileAccess.WRITE)
			if file==null:
				server.stop()
				return false
			file.store_string(JSON.stringify({"port":port,"token":token}))
			file.close()
			return true
	return false

func stop() -> void:
	server.stop()
	for c in clients: c.peer.disconnect_from_host()
	clients.clear()
	if connection_file!="": DirAccess.remove_absolute(connection_file)
	connection_file=""
	token=""

func _process(_delta: float) -> void:
	if not server.is_listening(): return
	while server.is_connection_available():
		var peer=server.take_connection()
		if clients.size()>=4: peer.disconnect_from_host()
		else: clients.append({"peer":peer,"buffer":"","created":Time.get_ticks_msec()})
	for c in clients.duplicate():
		c.peer.poll()
		if c.peer.get_status()!=StreamPeerTCP.STATUS_CONNECTED or Time.get_ticks_msec()-c.created>5000:
			c.peer.disconnect_from_host()
			clients.erase(c)
			continue
		var available=c.peer.get_available_bytes()
		if available>0:
			if available+c.buffer.length()>20000:
				c.peer.disconnect_from_host()
				clients.erase(c)
				continue
			c.buffer+=c.peer.get_utf8_string(available)
		if "\n" in c.buffer:
			var message=JSON.parse_string(c.buffer.get_slice("\n",0))
			var response={"error":"인증되지 않은 연결입니다."}
			if message is Dictionary and message.get("token")==token:
				if message.get("method")=="observe": response=session.observe()
				elif message.get("method")=="act" and message.get("action") is Dictionary:
					response=session.command(message.action)
				else: response={"error":"허용되지 않은 요청입니다."}
			c.peer.put_data((JSON.stringify(response)+"\n").to_utf8_buffer())
			clients.erase(c)
			# Closing the reference flushes the already written response.

func _exit_tree() -> void:
	stop()
