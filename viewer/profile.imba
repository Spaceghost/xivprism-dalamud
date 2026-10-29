export const panels = ['player','target','party','sources']
export const defaults = {
	Peripheral: {player:{x:.01,y:.02,w:.29,h:.28},target:{x:.70,y:.02,w:.29,h:.28},party:{x:.01,y:.60,w:.29,h:.38},sources:{x:.70,y:.60,w:.29,h:.38}},
	Desk: {player:{x:.02,y:.02,w:.46,h:.30},target:{x:.52,y:.02,w:.46,h:.30},party:{x:.02,y:.36,w:.46,h:.61},sources:{x:.52,y:.36,w:.46,h:.61}}
}
export def validProfiles(value)
	if !value || value.version != 1 || !value.profiles || !Object.keys(value.profiles).length || Object.keys(value.profiles).length > 16
		return false
	for [name,layout] of Object.entries(value.profiles)
		if !name.trim() || name.length > 64 || ['__proto__','constructor','prototype'].includes(name) || !layout || Object.keys(layout).length != 4
			return false
		for key of panels
			const r = layout[key]
			if !r || !['x','y','w','h'].every(do(k) typeof r[k] == 'number' && Number.isFinite(r[k])) || r.x < 0 || r.y < 0 || r.w < .1 || r.h < .1 || r.x + r.w > 1.001 || r.y + r.h > 1.001
				return false
	return true
