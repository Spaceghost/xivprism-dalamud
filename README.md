# XivPrism

Your HUD, wherever you look.

A working Dalamud publisher and standalone browser viewer for the illustrated UI
family. Pair a viewer to display player and target HP/MP, party readings, module
readiness, Wayfinder guidance and Piano's public now-playing metadata. The feed
is read-only; chat bodies, credentials, stream URLs and game controls are excluded.

This development version targets FFXIV integration and the standalone viewer.
Native Moonlight composition, platform-specific native apps, display enumeration,
hotplug and glasses tracking are separate milestones. No hardware support is
implied by opening the viewer in a browser.

## Use

Install the manual development package, then open `/xivprism`. Enable the viewer
and open its displayed address in a browser. Enter the six-digit code from the
game. Codes expire after five minutes and rotate after successful pairing; viewer
sessions expire after twelve hours. Logout, stopping the publisher, reloading the
plugin or **Revoke all viewers** invalidates viewer access.

The default listener is loopback-only on port 41830. For another device, explicitly
enable private-network access and configure the advertised host address. Use a
trusted private network or encrypted tunnel: the built-in transport is HTTP.
Pairing codes and bearer capabilities must be kept private. Unknown host headers
and cross-origin browser requests are refused. The publisher has no cloud service. Its bounded socket framing is adapted from XivMcp, avoiding Windows http.sys and administrator URL reservations.

The viewer offers peripheral and desk layouts, full screen, panel arrangement,
local profiles and versioned import/export. Layouts belong to the receiving
browser; only layout settings are exported. Camp can recommend a named local
profile, which the viewer applies when you choose it. Game facts are never stored
in layout exports. Stale, logged-out and disconnected facts are hidden after two
seconds; there is no simulated fallback in the live viewer.

`Umbra.XivPrism.dll` adds the optional Umbra toolbar entry. Add its path in Umbra
Settings → Plugins, restart Umbra and choose the widget in the toolbar editor.
Left-click toggles settings; right-click opens settings. Existing UI modules and
Floating Windows keep their own rendering, input and hosting ownership.

## Build

Node 22+, Python 3 and .NET SDK 10.0.401:

```sh
npm ci --ignore-scripts
tools/fetch-dalamud.sh
python3 tools/plugin-build.py --test --locked
npm run check
npm test
python3 tools/plugin-package.py
```

For installed references, use `--references /path/to/reviewed/dalamud`. Add
`--umbra-references /path/to/installed/Umbra` to build the optional adapter.
Reviewed reference hashes are checked. ZIPs include only owned assemblies and
the manifest; game-provided DLLs are excluded. `build-info.json` records provenance.

Core tests include socket HTTP pairing, unauthorized requests, origin
checks, expiry, revocation, snapshot bounds, stale-data hiding and logout. Browser
logic tests check profile validation. Exact live evidence and limitations belong
in [acceptance](docs/ACCEPTANCE.md). [IPC](docs/IPC.md) describes local integration.

The earlier `preview/` and its artwork remain a separately labelled design demo
with invented data. Run `npm run build` to rebuild it; it does not become a live
connection merely because the new publisher is installed.

[Project](https://spacegho.st/mods/ffxiv/xivprism/) ·
[Umbra family](https://spacegho.st/mods/ffxiv/umbra/) · [Roadmap](ROADMAP.md).
