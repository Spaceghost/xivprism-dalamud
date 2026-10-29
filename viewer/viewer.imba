import {defaults,panels,validProfiles} from './profile.js'
const byId = do(id) document.getElementById(id)
let profiles = structuredClone(defaults)
let selected = 'Peripheral'
let token = ''
try
	token = sessionStorage.getItem('xivprism.viewer') || ''
catch
	undefined
let lastReceipt = 0
let stateAge = 0
let live = false
let arranging = false
let suggested = ''
try
	const saved = JSON.parse(localStorage.getItem('xivprism.layouts') || 'null')
	if validProfiles(saved)
		profiles = saved.profiles
catch
	undefined

def rememberToken(value)
	try
		if value
			sessionStorage.setItem('xivprism.viewer',value)
		else
			sessionStorage.removeItem('xivprism.viewer')
	catch
		undefined

def persist
	try
		localStorage.setItem('xivprism.layouts',JSON.stringify({version:1,profiles}))
	catch
		byId('connection').textContent = 'Layouts stay in memory here; export to keep a copy'

def layout
	for [index,key] of panels.entries()
		const card = document.querySelector('[data-panel="' + key + '"]')
		const r = profiles[selected][key]
		const mobile = innerWidth < 600
		card.style.left = (mobile ? 1 : r.x * 100) + '%'
		card.style.top = (mobile ? index * 25 : r.y * 100) + '%'
		card.style.width = (mobile ? 98 : r.w * 100) + '%'
		card.style.height = (mobile ? 24 : r.h * 100) + '%'

def choose
	byId('profile').replaceChildren()
	for name of Object.keys(profiles)
		const option = document.createElement('option')
		option.value = name
		option.textContent = name
		byId('profile').append(option)
	if !Object.hasOwn(profiles,selected)
		selected = Object.keys(profiles)[0]
	byId('profile').value = selected
	layout()

def quiet(message)
	live = false
	byId('canvas').hidden = true
	byId('idle').hidden = !token
	byId('connection').textContent = message
	for key of panels
		document.querySelector('[data-panel="' + key + '"] div').replaceChildren()

def line(parent,text,style='')
	const p = document.createElement('p')
	p.textContent = String(text)
	p.className = style
	parent.append(p)

def actor(parent,value)
	if !value
		line(parent,'No current reading','muted')
		return
	line(parent,value.name)
	line(parent,value.job + ' · Level ' + value.level,'muted')
	line(parent,'HP ' + value.hp.toLocaleString() + ' / ' + value.maxHp.toLocaleString())
	const bar = document.createElement('div')
	bar.className = 'resource'
	const fill = document.createElement('span')
	fill.style.width = (value.maxHp > 0 ? Math.max(0,Math.min(100,value.hp / value.maxHp * 100)) : 0) + '%'
	bar.append(fill)
	parent.append(bar)
	line(parent,'MP ' + value.mp.toLocaleString() + ' / ' + value.maxMp.toLocaleString(),'muted')

def render(frame)
	live = true
	byId('canvas').hidden = false
	byId('idle').hidden = true
	byId('pairing').hidden = true
	byId('connection').textContent = 'Live · read-only'
	const contents = Object.fromEntries(panels.map(do(key) [key,document.querySelector('[data-panel="' + key + '"] div')]))
	Object.values(contents).forEach(do(node) node.replaceChildren())
	actor(contents.player,frame.player)
	actor(contents.target,frame.target)
	for member of frame.party
		line(contents.party,member.name + ' · ' + member.job + ' · HP ' + member.hp.toLocaleString() + '/' + member.maxHp.toLocaleString())
	if !frame.party.length
		line(contents.party,'Travelling on your own','muted')
	for source of frame.sources
		line(contents.sources,source.name + ' · ' + (source.ready ? 'ready · ' + source.mode : source.available ? 'waiting' : 'not installed') + (source.count ? ' · ' + source.count + ' quests' : '') + (source.info ? ' · ' + source.info : ''),source.ready ? 'ready' : 'muted')
	byId('scene').textContent = frame.scene ? 'Camp · ' + frame.scene : ''
	suggested = frame.profile || ''
	byId('recommendation').hidden = !Object.hasOwn(profiles,suggested) || suggested == selected
	byId('recommendation').textContent = 'Use Camp’s ' + suggested + ' layout'

byId('pair').addEventListener('submit',do(event)
	event.preventDefault()
	byId('pair-error').textContent = ''
	try
		const response = await fetch('/api/pair',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({code:byId('code').value}),signal:AbortSignal.timeout(3000)})
		if !response.ok
			throw new Error('Code expired or refused. Get a new code from the game and try again.')
		const result = await response.json()
		token = result.token
		rememberToken(token)
		byId('code').value = ''
		byId('pairing').hidden = true
	catch error
		byId('pair-error').textContent = error.message
)
def poll
	try
		if token && !document.hidden
			const response = await fetch('/api/state',{headers:{Authorization:'Bearer ' + token},cache:'no-store',signal:AbortSignal.timeout(2000)})
			if response.status == 401
				token = ''
				rememberToken('')
				byId('pairing').hidden = false
				quiet('Pair again · viewer expired or revoked')
			else
				if !response.ok
					throw new Error('Publisher unavailable')
				const result = await response.json()
				if result.version != 1 || !result.live || !result.state || result.ageMs > 2000
					quiet('Waiting for fresh game data')
				else
					lastReceipt = performance.now()
					stateAge = Math.max(0,result.ageMs)
					render(result.state)
	catch
		quiet('Disconnected · details hidden')
	finally
		setTimeout(poll,500)
setInterval((do
	if live && (document.hidden || performance.now() - lastReceipt + stateAge > 2000)
		quiet('Stale · details hidden')
),200)
byId('profile').addEventListener('change',do
	selected = byId('profile').value
	layout()
)
byId('recommendation').addEventListener('click',do
	if Object.hasOwn(profiles,suggested)
		selected = suggested
		byId('profile').value = selected
		layout()
		byId('recommendation').hidden = true
)
byId('arrange').addEventListener('click',do
	arranging = !arranging
	document.body.classList.toggle('arranging',arranging)
	byId('arrange').textContent = arranging ? 'Finish arranging' : 'Arrange panels'
)
byId('save').addEventListener('click',do
	const name = prompt('Name this device’s layout',selected)
	if !name || name.length > 64 || ['__proto__','constructor','prototype'].includes(name) || (!Object.hasOwn(profiles,name) && Object.keys(profiles).length >= 16)
		return
	const canvas = byId('canvas').getBoundingClientRect()
	if !canvas.width || !canvas.height
		return
	const result = {}
	for key of panels
		const r = document.querySelector('[data-panel="' + key + '"]').getBoundingClientRect()
		const w = Math.max(.1,Math.min(1,r.width / canvas.width))
		const h = Math.max(.1,Math.min(1,r.height / canvas.height))
		result[key] = {x:Math.max(0,Math.min(1 - w,(r.left - canvas.left)/canvas.width)),y:Math.max(0,Math.min(1 - h,(r.top - canvas.top)/canvas.height)),w,h}
	profiles[name] = result
	selected = name
	persist()
	choose()
)
byId('forget').addEventListener('click',do
	token = ''
	rememberToken('')
	byId('pairing').hidden = false
	quiet('Disconnected')
)
byId('full').addEventListener('click',do
	try
		if document.fullscreenElement
			await document.exitFullscreen()
		else
			await document.documentElement.requestFullscreen()
	catch
		byId('connection').textContent = 'Full screen is unavailable in this browser'
)
byId('export').addEventListener('click',do
	const url = URL.createObjectURL(new Blob([JSON.stringify({version:1,profiles},null,2)],{type:'application/json'}))
	const a = document.createElement('a')
	a.href = url
	a.download = 'xivprism-layouts.json'
	a.click()
	setTimeout((do URL.revokeObjectURL(url)),1000)
)
byId('import').addEventListener('change',do
	try
		const file = byId('import').files[0]
		if !file || file.size > 65536
			throw new Error('Choose a layout file smaller than 64 KiB')
		const value = JSON.parse(await file.text())
		if !validProfiles(value)
			throw new Error('Unsupported or invalid layout file')
		profiles = value.profiles
		persist()
		choose()
	catch error
		byId('connection').textContent = error.message
)
for card of document.querySelectorAll('[data-panel]')
	card.querySelector('h2').addEventListener('pointerdown',do(event)
		if !arranging
			return
		const start = {x:event.clientX,y:event.clientY,left:card.offsetLeft,top:card.offsetTop}
		card.querySelector('h2').setPointerCapture(event.pointerId)
		const move = do(e)
			card.style.left = Math.max(0,Math.min(byId('canvas').clientWidth - card.offsetWidth,start.left + e.clientX - start.x)) + 'px'
			card.style.top = Math.max(0,Math.min(byId('canvas').clientHeight - card.offsetHeight,start.top + e.clientY - start.y)) + 'px'
		const end = do
			card.querySelector('h2').removeEventListener('pointermove',move)
		card.querySelector('h2').addEventListener('pointermove',move)
		card.querySelector('h2').addEventListener('pointerup',end,{once:true})
	)
addEventListener('resize',layout)
choose()
byId('pairing').hidden = !!token
quiet(token ? 'Connecting…' : 'Not paired')
poll()

document.addEventListener('visibilitychange',do
	if document.hidden
		quiet('Paused while this viewer is hidden')
)
