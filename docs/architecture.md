# XivPrism architecture

The Dalamud publisher produces versioned snapshots. Paired viewers own display enumeration, layouts, hotplug and rendering. The game host never needs to enumerate remote-client glasses. The standalone viewer and native Moonlight presentation share a renderer and layout contract. Game facts carry freshness and source identity; missing or stale sources do not masquerade as live data. Hardware pinning and spatial tracking are optional adapters, never baseline assumptions. Android and iPadOS application lifecycle and compositing constraints require real-device tests.

## Acceptance before release

- Distinguish simulated state from live state in every viewer.
- Record capabilities and failures instead of claiming unsupported behavior.
- Recover after disconnect, unload or unavailable optional providers.
- Preserve explicit input ownership and deliberate write actions.
- Measure performance on the actual target devices.
- Publish exact build, platform and hardware verification records.
