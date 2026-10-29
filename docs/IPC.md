# Versioned local interfaces

The public JSON call gate is `XivPrism.v1.Call`. `api.version` reports version
and methods; unknown methods are refused. `status` reads cached, bounded state.
UI mutations are queued; they do not run inside an IPC callback. The optional
Umbra adapter uses `XivPrism.Toggle`, `.OpenSettings`, and `.Ready`. Provider
assembly references never cross these boundaries.

`window.open`, `window.close` and `profile.recommend` are supported. A profile
recommendation takes bounded `scene` and `profile` strings; it cannot alter the
receiving device's saved layouts. Status never contains a pairing code or viewer
token. The authenticated `/api/state` feed is separate from MCP and is read-only.
