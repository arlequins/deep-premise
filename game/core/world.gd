class_name SimWorld
extends RefCounted

const PLACES = [
	{"id":"hall","name":"공동회관","x":0.47,"y":0.48},
	{"id":"market","name":"세 갈래 시장","x":0.65,"y":0.36},
	{"id":"farm","name":"남쪽 경작지","x":0.30,"y":0.78},
	{"id":"dock","name":"물안개 나루","x":0.79,"y":0.68},
	{"id":"loom","name":"직조 마당","x":0.24,"y":0.44},
	{"id":"archive","name":"오래된 장부실","x":0.45,"y":0.23},
	{"id":"shrine","name":"낮은 종의 뜰","x":0.19,"y":0.22},
	{"id":"gate","name":"북문","x":0.73,"y":0.14}]
const NAMES = ["이안","마라","세온","루아","도안","네리","유나","라온","오린","다린","소하","에린"]
const HOUSES = ["여울","자작","푸른실","서리","등불","갈대"]
const JOBS = ["경작인","경작인","상인","직조공","운반인","기록원","문지기","수리공","제빵사"]
const WORK = ["farm","farm","market","loom","dock","archive","gate","hall","market"]
var s: Dictionary = {}

func start(seed_value: int = 271828) -> void:
	s = {"version":2,"rng":maxi(1,seed_value),"seed":seed_value,"tick":8,"next_event":1,"next_report":1,
		"people":[],"events":[],"pending":[],"reports":[],"treasury":90.0,"stores":130.0,"market_food":170.0,
		"price":2.0,"rain":0.7,"soil":0.86,"repair":0.68,"policy":"balanced","route":"river",
		"public_policy":"open","representative":5,"notes":"","tracked":[0,5,14],"errands":[],
		"tactical":{},"last_convoy":-999,"last_seen":8,"last_petition":-999,"obligations":[]}
	for i in range(72):
		var j = i % 9
		var p = {"id":i,"name":HOUSES[i/12]+" "+NAMES[i%12],"house":i/12,"species":"이어인" if i%4==0 else "사람",
			"job":JOBS[j],"work":WORK[j],"location":WORK[j],"money":8+rand()*24,"food":2+rand()*4,
			"hunger":0.0,"health":1.0,"trust":0.3+rand()*0.5,"ambition":rand(),"belief":rand(),
			"ties":{},"memory":[],"partner":-1,"alive":true,"resting":0,"faction":i%3,"continuity":1}
		p.ties[str((i+1)%72)] = 0.45
		p.ties[str((i+13)%72)] = rand()*0.6-0.25
		s.people.append(p)
	report({},"네리 · 회관의 기록원","책상은 창가 쪽입니다. 돌아오실 때는 먼저 편지함을 봐 주세요. 급한 일은 정해 둔 방침대로 처리할게요.",0,"important",5)
	report({},"전임자의 쪽지","식량은 회관 몫만 적어 두었습니다. 시장 사정은 직접 물어야 해요. 그리고 이어인에게는 나이보다 이번에는 어디서 지내는지 묻는 편이 좋습니다.")
	deliver()

func rand() -> float:
	var x = int(s.rng)
	x = (x ^ (x << 13)) & 0xffffffff
	x = (x ^ (x >> 17)) & 0xffffffff
	x = (x ^ (x << 5)) & 0xffffffff
	s.rng = x
	return float(x)/4294967296.0

func choose(items: Array):
	return items[int(rand()*items.size())] if not items.is_empty() else null

func event(kind: String, actors: Array, place: String, facts: Dictionary = {}) -> Dictionary:
	var e = {"id":"e%d"%int(s.next_event),"tick":s.tick,"kind":kind,"actors":actors,"place":place,"facts":facts}
	s.next_event += 1
	s.events.append(e)
	if s.events.size()>2500: s.events.pop_front()
	return e

func report(e: Dictionary, source: String, text: String, delay: int = 0, importance: String = "ordinary", subject: int = -1) -> void:
	s.pending.append({"id":"r%d"%int(s.next_report),"event_id":e.get("id",""),"source":source,"text":text,
		"written":s.tick,"arrives":s.tick+delay,"importance":importance,"subject":subject,"read":false,"investigated":false})
	s.next_report += 1

func deliver() -> void:
	for r in s.pending.duplicate():
		if r.arrives<=s.tick:
			s.reports.append(r)
			s.pending.erase(r)
	while s.reports.size()>300: s.reports.pop_front()

func remember(p: Dictionary, e: Dictionary, feeling: String) -> void:
	p.memory.append({"event_id":e.id,"feeling":feeling})
	if p.memory.size()>40: p.memory.pop_front()

func advance(hours: int = 1) -> void:
	for step in range(clampi(hours,0,24000)):
		s.tick += 1
		var hour = int(s.tick)%24
		for p in s.people:
			if not p.alive: continue
			if p.resting>0: p.location="hall"
			elif hour<6 or hour>=21: p.location=["loom","hall","dock","market","shrine","farm"][int(p.house)]
			elif hour==12: p.location="shrine"
			elif hour>=17: p.location=s.people[int(p.partner)].work if p.partner>=0 else "market"
			else: p.location=p.work
		if hour==8: economy()
		if hour in [10,17,19]: social()
		if not s.tactical.is_empty() and s.tick>=s.tactical.next_auto: battle_turn(true)
		for q in s.errands.duplicate():
			if q.at<=s.tick:
				investigation_reply(q)
				s.errands.erase(q)
		deliver()

func social() -> void:
	var people = s.people.filter(func(p): return p.alive and p.resting==0)
	for n in range(12):
		var a = choose(people)
		if a==null: return
		var local = people.filter(func(p): return p.id!=a.id and p.location==a.location)
		if local.is_empty(): continue
		var known = local.filter(func(p): return a.ties.get(str(p.id),0)>0.2 or p.house==a.house)
		var b = choose(known if not known.is_empty() and rand()<0.7 else local)
		var rel = a.ties.get(str(b.id),0.0)
		if b.hunger>0.7 and a.food>2:
			a.food-=1
			b.food+=1
			a.ties[str(b.id)]=clampf(rel+0.14,-1,1)
			b.ties[str(a.id)]=clampf(b.ties.get(str(a.id),0.0)+0.24,-1,1)
			var e=event("gift",[a.id,b.id],a.location,{"amount":1})
			remember(b,e,"은혜")
			if rand()<0.25: report(e,b.name+" · 짧은 편지",a.name+" 덕에 오늘은 같이 먹었습니다. 회관에도 인사를 전하고 싶었습니다.",3,"ordinary",a.id)
		elif rel>0.5 and a.partner<0 and b.partner<0 and rand()<0.16:
			a.partner=b.id
			b.partner=a.id
			var e=event("love",[a.id,b.id],a.location)
			remember(a,e,"애정")
			remember(b,e,"애정")
			if rand()<0.55: report(e,"시장 찻집에서 들은 이야기",a.name+"와 "+b.name+", 요즘 같은 잔을 번갈아 쓴대요. 장사 이야기만 하는 것 같지는 않던데.",18+int(rand()*72),"ordinary",a.id)
		elif a.hunger>1.8 and b.money>10 and rand()<0.22:
			b.money-=5
			a.money+=5
			var e=event("theft",[a.id,b.id],a.location,{"amount":5})
			remember(a,e,"수치")
			report(e,b.name+" · 분실 신고","주머니가 사라졌습니다. 누가 가져갔는지는 보지 못했습니다.",6,"ordinary",b.id)
		else:
			var affinity=(0.09 if a.faction==b.faction else -0.025)+(0.025 if a.work==b.work else 0.0)
			a.ties[str(b.id)]=clampf(rel+affinity+(rand()-0.4)*0.12,-1,1)
			if rel<=0.5 and a.ties[str(b.id)]>0.5:
				b.ties[str(a.id)]=maxf(b.ties.get(str(a.id),0.0),0.45)
				var e=event("friendship",[a.id,b.id],a.location)
				remember(a,e,"친밀함")
				report(e,b.name+" · 이웃의 소식",a.name+"와 일을 마치고 차를 마셨습니다. 다음번에는 의자를 하나 더 놓기로 했어요.",6,"ordinary",a.id)
			if a.ties[str(b.id)]< -0.45 and rand()<0.14:
				var e=event("quarrel",[a.id,b.id],a.location)
				remember(a,e,"원한")
				report(e,"나루에서 전해 들은 말",a.name+"와 "+b.name+"가 또 언성을 높였답니다. 배분 문제라는데, 전부터 사이가 좋지는 않았지요.",12,"ordinary",a.id)

func economy() -> void:
	s.rain=clampf(s.rain+(rand()-0.5)*0.38+(0.62-s.rain)*0.08,0.12,1)
	s.soil=clampf(s.soil+(s.rain-0.5)*0.025+(0.8-s.soil)*0.03,0.35,1)
	s.repair=clampf(s.repair-0.008+(0.035 if s.policy=="repair" else 0.0),0.1,1)
	var alive=s.people.filter(func(p): return p.alive)
	var farmers=alive.filter(func(p): return p.work=="farm" and p.resting==0 and p.hunger<3)
	var boats=alive.filter(func(p): return p.work=="dock" and p.resting==0)
	var harvest=farmers.size()*(4.8+s.rain*2.1)*s.soil*(0.75+s.repair*0.25)
	var imports=boats.size()*(0.7+s.repair)*(1.15 if s.route=="river" else 1.0)
	s.market_food=minf(900,s.market_food+harvest+imports)
	s.price=clampf(1.3+100/maxf(25,s.market_food)+(1-s.repair),1.5,7)
	var fed=0
	var shift=int(rand()*alive.size())
	for i in range(alive.size()):
		var p=alive[(i+shift)%alive.size()]
		p.resting=maxi(0,int(p.resting)-1)
		var wage=3.1 if p.work=="market" else 1.75 if p.work=="farm" else 2.15
		p.money=maxf(0,p.money+(0.4 if p.resting>0 else wage*(0.55 if p.hunger>2 else 1.0))-0.4)
		var buy=minf(maxf(0,2.2-p.food),minf(p.money/s.price,s.market_food))
		p.food+=buy
		p.money-=buy*s.price
		s.market_food-=buy
		if p.food<1 and s.stores>=1 and (s.policy=="relief" or p.trust>0.55):
			s.stores-=1
			p.food+=1
			p.trust=clampf(p.trust+0.025,0,1)
			fed+=1
		var eaten=minf(1,p.food)
		p.food-=eaten
		p.hunger=clampf(p.hunger+(1-eaten)*0.9-eaten*0.55,0,6)
		p.health=clampf(p.health+(-0.055 if p.hunger>2 else 0.018),0,1)
		p.trust=clampf(p.trust+(0.005 if eaten>=0.99 else -0.045),0,1)
		if p.hunger>0.5 and p.hunger<2 and p.money<1 and rand()<0.12:
			var lenders=alive.filter(func(q): return q.id!=p.id and q.money>18 and (q.house==p.house or q.ties.get(str(p.id),0)>0.3))
			var debts=s.obligations.filter(func(o): return o.debtor==p.id and not o.paid)
			if not lenders.is_empty() and debts.is_empty():
				var lender=lenders[0]
				lender.money-=6
				p.money+=6
				var e=event("loan",[lender.id,p.id],p.location,{"amount":6})
				remember(p,e,"빚")
				s.obligations.append({"debtor":p.id,"creditor":lender.id,"due":s.tick+168,"paid":false})
				if rand()<0.4: report(e,p.name+" · 회관에 맡긴 쪽지",lender.name+"에게 빌린 여섯 닢을 장부에 남겨 주세요. 추수가 끝나면 돌려드리겠습니다.",8,"ordinary",p.id)
		if p.health<=0:
			p.alive=false
			var e=event("death",[p.id],p.location)
			report(e,"낮은 종의 뜰 · 부고",p.name+"의 자리가 비었습니다. 유족은 내일 해 질 무렵 뜰에서 인사를 받습니다.",10,"important",p.id)
			for q in alive:
				if q.ties.get(str(p.id),0)>0.3: remember(q,e,"상실")
	for o in s.obligations:
		if o.paid or o.due>s.tick: continue
		var debtor=s.people[int(o.debtor)]
		var creditor=s.people[int(o.creditor)]
		if debtor.alive and creditor.alive and debtor.money>9:
			debtor.money-=6
			creditor.money+=6
			o.paid=true
			var e=event("repayment",[debtor.id,creditor.id],"archive")
			creditor.ties[str(debtor.id)]=clampf(creditor.ties.get(str(debtor.id),0.0)+0.2,-1,1)
			report(e,"장부실 · 수령 확인",creditor.name+"가 "+debtor.name+"의 여섯 닢을 받았습니다. 남아 있던 줄을 지웠습니다.",8,"ordinary",debtor.id)
		elif debtor.alive and s.tick-o.due>168 and int(s.tick)%168==8:
			creditor.ties[str(debtor.id)]=clampf(creditor.ties.get(str(debtor.id),0.0)-0.1,-1,1)
	s.obligations=s.obligations.filter(func(o): return not o.paid or s.tick-o.due<720)
	var day=int(s.tick)/24
	for p in alive:
		if p.alive and p.species=="이어인" and day%60==20+int(p.id)%35:
			p.continuity+=1
			p.memory=p.memory.slice(-3)
			p.resting=2
			for id in p.ties:
				if s.people[int(id)].house!=p.house: p.ties[id]*=0.35
			var e=event("renewal",[p.id],"shrine")
			report(e,p.name+"의 집에서 온 편지","오늘 "+p.name+"의 자리를 다시 차렸습니다. 익숙한 일을 하기는 하지만 방문객들의 얼굴 앞에서 잠깐씩 망설입니다.",16,"important",p.id)
	var levy=minf(s.market_food,6 if s.public_policy=="reserve" else 3)
	s.market_food-=levy
	s.stores=minf(360,s.stores+levy)
	s.treasury=minf(260,s.treasury+4-(3 if s.policy=="repair" else 0))
	if int(s.tick)%72==8:
		var e=event("market",[],"market",{"price":s.price,"harvest":harvest})
		report(e,"세 갈래 시장 · 가격표","오늘 보리 한 몫은 은화 %.1f닢입니다. "%s.price+("제빵사들이 빵의 크기를 줄였습니다." if s.price>3 else "나루 쪽 가게에 갓 구운 빵이 나왔습니다."),4)
	if fed>5: report({},"회관 배식 담당","오늘은 %d명이 따뜻한 그릇을 받아 갔습니다. 남은 빵은 천으로 덮어 두었습니다."%fed)
	if s.stores<35 and s.tick-s.last_convoy>96 and s.tactical.is_empty() and s.treasury>=12: start_convoy(true)
	if day%7==0 and not alive.is_empty():
		var p=choose(alive)
		var e=event("gathering",[p.id],"shrine")
		report(e,p.name+" · 뜰에서 온 초대","이번 저녁에는 남은 과일로 술을 빚습니다. 일찍 오는 분은 의자를 가져와 주세요. 지난번 노래는 금지랍니다." if s.market_food>100 else "이번에는 음식 없이 모이기로 했습니다. 그래도 같이 앉을 자리는 있습니다.",7,"ordinary",p.id)
	if day%14==0: politics(alive)
	if day%20==0: history()

func politics(alive: Array) -> void:
	var votes=[0,0,0]
	for p in alive: votes[1 if p.hunger>1 else 2 if p.trust<0.35 else int(p.faction)]+=1
	var faction=votes.find(votes.max())
	var candidates=alive.filter(func(p): return p.faction==faction)
	if candidates.is_empty(): return
	candidates.sort_custom(func(a,b): return a.ambition>b.ambition)
	var leader=candidates[0]
	s.representative=leader.id
	s.public_policy="reserve" if faction==1 else "open"
	var e=event("election",[leader.id],"hall",{"votes":votes})
	report(e,"회합의 서명부",leader.name+"가 이번 회합의 대표를 맡습니다. "+("공동 비축분을 늘리자는 안이 통과되었습니다." if faction==1 else "장터의 통행을 지금처럼 열어 두기로 했습니다." if faction==0 else "회관의 장부를 더 자주 열람하자는 요구가 나왔습니다."),2,"important",leader.id)

func history() -> void:
	var old=s.events.filter(func(e): return s.tick-e.tick>120 and e.kind in ["gift","convoy","love","quarrel"])
	if old.is_empty(): return
	var e=choose(old)
	var p=s.people[int(e.actors[0])]
	report(e,"장부실 · 지난 계절의 정리",p.name+"의 이름이 오래된 쪽지에 다시 나옵니다. «그날 이후 자리가 달라졌다»고 적혀 있지만, 날짜 옆에는 다른 필체의 수정이 있습니다.",12,"important",p.id)

func find_report(id: String) -> Dictionary:
	for r in s.reports:
		if r.id==id: return r
	return {}

func investigation_reply(q: Dictionary) -> void:
	var found=s.events.filter(func(e): return e.id==q.event_id)
	if found.is_empty():
		report({},"장부실 · 회신","관련된 원본을 찾지 못했습니다. 보관된 문서에는 더 이상 이어지는 이름이 없습니다.")
		return
	var e=found[0]
	var a=s.people[int(e.actors[0])] if not e.actors.is_empty() else {}
	var b=s.people[int(e.actors[1])] if e.actors.size()>1 else {}
	var text=""
	match e.kind:
		"theft": text=b.name+"의 신고에는 사라진 돈만 적혀 있습니다. 그날 장터에 있던 사람들의 진술은 서로 맞지 않습니다."
		"gift": text=b.name+"가 보관한 쪽지에는 "+a.name+"의 이름과 «한 그릇은 갚지 않아도 된다»는 문장이 있습니다."
		"love": text=a.name+"는 사적인 일이라고 했습니다. "+b.name+"에게 보내는 편지를 대신 맡아 달라는 부탁은 했습니다."
		"market": text="그날 제출된 가격표도 %.1f닢을 적고 있습니다. 경작지 기록은 아직 들어오지 않았습니다."%e.facts.price
		"convoy": text=a.name+"의 귀환 장부와 창고의 입고량은 %d몫으로 일치합니다. 길 위에서 누가 먼저 소리쳤는지는 기록하지 않았습니다."%int(e.facts.cargo)
		"renewal": text="일을 맡기던 이웃은 여전히 같은 이름을 씁니다. 집안의 편지에는 «방문 전에 한 번 더 소개해 달라»는 부탁이 있습니다."
		"loan", "repayment": text="장부에는 여섯 닢과 두 사람의 서명이 남았습니다. 그 밖의 약속은 말로만 나누었다고 합니다."
		_: text=a.get("name","당사자")+"에게 이야기를 청했습니다. «밖에서 전해지는 말과는 사정이 조금 다릅니다.» 다음에 다시 만나자는 답을 받았습니다."
	report(e,"회관 조사 담당 · 회신",text,0,"important",a.get("id",-1))

func start_convoy(automatic: bool = false) -> String:
	if not s.tactical.is_empty(): return "이미 진행 중인 호송이 있습니다."
	if s.treasury<12: return "호송 준비에는 은화 12닢이 필요합니다."
	if s.tick-s.last_convoy<48: return "지난 호송의 인원들이 아직 쉬고 있습니다."
	var guards=s.people.filter(func(p): return p.alive and p.resting==0 and p.work in ["gate","dock"]).slice(0,3)
	if guards.size()<3: return "호송에 나설 인원이 부족합니다."
	s.treasury-=12
	s.last_convoy=s.tick
	var units=[]
	for i in range(3): units.append({"id":guards[i].id,"x":0,"y":i+1,"hp":3,"ap":2})
	s.tactical={"round":1,"next_auto":s.tick+6,"wagon":{"x":0,"y":2,"hp":5},"units":units,
		"enemies":[{"id":"b0","x":5,"y":1,"hp":2},{"id":"b1","x":5,"y":3,"hp":2}],
		"cover":[{"x":2,"y":1},{"x":3,"y":3},{"x":4,"y":1}] if s.route=="river" else [{"x":2,"y":3},{"x":4,"y":2}],
		"threat":clampf((1-s.repair)*0.6+s.price/12,0.2,0.85),"log":"수레 곁을 지키며 동쪽 끝까지 호송하세요."}
	report({},guards[0].name+" · 호송대",("정해 둔 방침에 따라 식량을 받으러 나왔습니다. " if automatic else "")+"북문 밖 통로에 사람들이 모여 있습니다. 직접 지휘가 없으면 여섯 시간 뒤부터 현장 판단으로 움직이겠습니다.",0,"urgent",guards[0].id)
	return ""

func distance(a: Dictionary,b: Dictionary) -> int:
	return absi(int(a.x)-int(b.x))+absi(int(a.y)-int(b.y))

func battle_turn(automatic: bool = false) -> void:
	if s.tactical.is_empty(): return
	var t=s.tactical
	if automatic:
		for u in t.units:
			if u.hp<=0: continue
			var enemies=t.enemies.filter(func(e): return e.hp>0 and distance(u,e)<=2)
			if not enemies.is_empty(): enemies[0].hp-=1
			else: u.x=mini(6,int(u.x)+1)
	for e in t.enemies:
		if e.hp<=0: continue
		var targets=t.units.filter(func(u): return u.hp>0)
		targets.append(t.wagon)
		targets.sort_custom(func(a,b): return distance(e,a)<distance(e,b))
		var target=targets[0]
		if distance(e,target)<=2:
			var covered=t.cover.any(func(c): return c.x==target.x and c.y==target.y)
			if rand()<(0.28 if covered else 0.62)+t.threat*0.15: target.hp-=1
		elif e.x!=target.x: e.x+=signi(int(target.x)-int(e.x))
		else: e.y+=signi(int(target.y)-int(e.y))
	var blocked=t.enemies.any(func(e): return e.hp>0 and distance(e,t.wagon)<=2)
	var escorted=t.units.any(func(u): return u.hp>0 and distance(u,t.wagon)<=2)
	if not blocked and escorted: t.wagon.x=mini(6,int(t.wagon.x)+1)
	t.round+=1
	t.next_auto=s.tick+3
	for u in t.units: u.ap=2
	t.log="통로가 막혀 수레가 기다립니다." if blocked else "수레가 동쪽으로 움직였습니다." if escorted else "수레 곁에 호위가 필요합니다."
	if t.wagon.hp<=0 or t.units.all(func(u): return u.hp<=0) or t.round>14: finish_convoy(false)
	elif t.wagon.x>=6: finish_convoy(true)

func finish_convoy(success: bool, retreat: bool = false) -> void:
	var t=s.tactical
	var cargo=int(65*maxf(0.4,t.wagon.hp/5.0)) if success else 12 if retreat else 0
	s.stores+=cargo
	var actors=[]
	for u in t.units:
		actors.append(u.id)
		var p=s.people[int(u.id)]
		p.resting=maxi(1,4-int(u.hp))
		p.health=maxf(0.3,p.health-(3-u.hp)*0.12)
		p.trust=clampf(p.trust+(0.1 if success else -0.1),0,1)
	var e=event("convoy",actors,"gate",{"cargo":cargo,"success":success})
	for id in actors: remember(s.people[int(id)],e,"자부심" if success else "두려움")
	var text="수레가 도착했습니다. 보리 %d몫을 창고에 들였습니다. 다친 사람들은 회관에서 쉬게 하겠습니다."%cargo if success else "사람들을 먼저 데리고 돌아왔습니다. 작은 자루 몇 개는 지켰습니다." if retreat else "수레를 두고 돌아왔습니다. 모두 돌아왔지만 당분간 쉬어야 합니다."
	report(e,s.people[int(actors[0])].name+" · 귀환 보고",text,0,"important",actors[0])
	s.tactical={}

func act(a: Dictionary) -> String:
	var kind=a.get("type","")
	match kind:
		"policy":
			if a.get("value") not in ["balanced","relief","repair"]: return "알 수 없는 방침입니다."
			s.policy=a.value
			report({},"회관의 업무 쪽지",{"balanced":"기존 배분과 비축 방침을 따릅니다.","relief":"배식 대상의 문턱을 낮춥니다.","repair":"하루 은화 3닢을 수로 보수에 배정합니다."}[a.value])
		"route":
			if a.get("value") not in ["river","road"]: return "알 수 없는 경로입니다."
			s.route=a.value
		"read", "investigate":
			var r=find_report(str(a.get("id","")))
			if r.is_empty(): return "문서를 찾지 못했습니다."
			if kind=="read": r.read=true
			else:
				if r.event_id=="" or r.investigated: return "더 조사할 수 없는 문서입니다."
				if s.treasury<3: return "조사에는 은화 3닢이 필요합니다."
				s.treasury-=3
				r.investigated=true
				s.errands.append({"event_id":r.event_id,"at":s.tick+12+int(rand()*18)})
		"track", "talk":
			var id=int(a.get("id",-1))
			if id<0 or id>=s.people.size(): return "주민을 찾지 못했습니다."
			var p=s.people[id]
			if kind=="track":
				if id in s.tracked: s.tracked.erase(id)
				else:
					s.tracked.append(id)
					if s.tracked.size()>12: s.tracked.pop_front()
			else:
				if not p.alive: return "지금 만날 수 없습니다."
				if s.reports.any(func(r): return r.source==p.name+" · 회관에서" and s.tick-r.written<24) or s.pending.any(func(r): return r.source==p.name+" · 회관에서"): return "오늘은 이미 안부를 청했습니다."
				var text="요즘은 일하고 돌아와도 식탁이 비어 있습니다. 우리 집만 그런가요?" if p.hunger>1 else "몸이 나으면 다시 나가겠습니다. 오늘은 조금만 쉬게 해 주세요." if p.resting>0 else "예전 손이 맺은 약속을 지금 손도 지켜야 한다고들 합니다. 저는 그 집 얼굴도 모르는데요." if p.species=="이어인" else "같이 저녁 먹을 사람이 생기니 집에 일찍 가게 되네요." if p.partner>=0 else "별일은 없어요. 이번 뜰 모임에는 오시나요? 의자가 하나 모자랄 것 같던데."
				report({},p.name+" · 회관에서",text,2,"ordinary",id)
		"petition":
			if s.treasury<8: return "공개 회합 준비에는 은화 8닢이 필요합니다."
			if s.tick-s.last_petition<72: return "다음 회합까지 조금 기다려 주세요."
			s.treasury-=8
			s.last_petition=s.tick
			for p in s.people: p.trust=clampf(p.trust+(0.12 if s.policy=="relief" and p.hunger>0 else 0.035),0,1)
			report({},"공개 회합 · 문지기의 쪽지","의자를 둥글게 놓았습니다. 배분 장부를 열어 두자 몇 사람이 앉아 질문을 시작했습니다.",3,"important")
		"notes":
			if not a.get("value") is String or a.value.length()>5000: return "메모는 5,000자까지 적을 수 있습니다."
			s.notes=a.value
		"seen": s.last_seen=s.tick
		"convoy":
			var error=start_convoy()
			if error!="": return error
		"move", "attack", "turn", "retreat":
			if s.tactical.is_empty(): return "진행 중인 호송이 없습니다."
			if kind=="turn": battle_turn()
			elif kind=="retreat": finish_convoy(false,true)
			else:
				var units=s.tactical.units.filter(func(u): return u.id==a.get("id",-1) and u.hp>0 and u.ap>0)
				if units.is_empty(): return "행동할 대원을 선택하세요."
				var u=units[0]
				if kind=="move":
					if not a.has("x") or not a.has("y"): return "이동할 칸을 선택하세요."
					if a.x!=int(a.x) or a.y!=int(a.y) or a.x<0 or a.x>6 or a.y<0 or a.y>4 or distance(u,a)!=1: return "인접한 칸에 이동할 수 있습니다."
					if (s.tactical.units+s.tactical.enemies).any(func(v): return v.hp>0 and v.x==a.x and v.y==a.y): return "이미 사람이 있는 자리입니다."
					u.x=a.x
					u.y=a.y
					u.ap-=1
				else:
					var enemies=s.tactical.enemies.filter(func(e): return e.id==a.get("target","") and e.hp>0 and distance(u,e)<=2)
					if enemies.is_empty(): return "두 칸 안의 상대를 선택하세요."
					enemies[0].hp-=1
					u.ap-=1
		_: return "허용되지 않은 명령입니다."
	deliver()
	return ""

func observe() -> Dictionary:
	var people=[]
	for p in s.people:
		people.append({"id":p.id,"name":p.name,"species":p.species,"job":p.job,"location":p.location,"alive":p.alive,
			"appearance":"빈 자리" if not p.alive else "쉬는 중" if p.resting>0 else "눈에 띄게 지쳐 있다" if p.hunger>2 else "일상을 보내는 중"})
	var reports=[]
	var unread=0
	var since=0
	for r in s.reports:
		var visible=r.duplicate(true)
		visible.erase("event_id")
		visible.can_investigate=r.event_id!="" and not r.investigated
		reports.push_front(visible)
		if not r.read: unread+=1
		if r.arrives>s.last_seen: since+=1
	var tactical=s.tactical.duplicate(true)
	if not tactical.is_empty():
		tactical.erase("threat")
		for u in tactical.units: u.name=s.people[int(u.id)].name
	return {"version":2,"tick":s.tick,"day":int(s.tick)/24+1,"hour":int(s.tick)%24,"treasury":int(s.treasury),"stores":int(s.stores),
		"policy":s.policy,"route":s.route,"representative":s.people[int(s.representative)].name,"notes":s.notes,
		"tracked":s.tracked.duplicate(),"places":PLACES.duplicate(true),"people":people,"reports":reports,
		"unread":unread,"since_visit":since,"investigations":s.errands.size(),"tactical":tactical}

func load_state(data) -> bool:
	if not data is Dictionary or data.get("version")!=2: return false
	for key in ["people","events","pending","reports","errands","obligations","tracked"]:
		if not data.get(key) is Array: return false
	for key in ["tick","rng","next_event","next_report","treasury","stores","market_food","price","rain","soil","repair","representative","last_convoy","last_seen","last_petition"]:
		if not (data.get(key) is float or data.get(key) is int) or not is_finite(float(data[key])): return false
	if data.people.size()!=72 or data.tick<0 or data.representative<0 or data.representative>=72: return false
	if data.policy not in ["balanced","relief","repair"] or data.route not in ["river","road"]: return false
	if not data.get("notes") is String or not data.get("tactical") is Dictionary: return false
	for i in range(72):
		var p=data.people[i]
		if not p is Dictionary or p.get("id")!=i or not p.get("ties") is Dictionary or not p.get("memory") is Array: return false
		for key in ["food","money","health","hunger","trust","ambition","belief","resting","partner","house","faction","continuity"]:
			if not (p.get(key) is float or p.get(key) is int) or not is_finite(float(p[key])): return false
		for key in ["name","species","job","work","location"]:
			if not p.get(key) is String: return false
		if not p.get("alive") is bool: return false
	s=data.duplicate(true)
	return true
