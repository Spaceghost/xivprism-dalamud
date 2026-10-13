# Compatibility targets

This is a target matrix, not a tested-hardware list. All live device capabilities remain unverified.

| Receiving device | Game source | Intended output |
| --- | --- | --- |
| Alienware | Fedora remote publisher or local FFXIV | Its monitors and attached glasses |
| Win Max 2 | Fedora remote publisher or local FFXIV | Its screen and attached glasses |
| Phone | Any paired game publisher | Phone screen and supported attached outputs |
| iPad | Any paired game publisher | Tablet and supported external presentation display |
| XREAL Beam | Any paired game publisher | Attached glasses; model/firmware must be identified |
| VITURE neckband | Any paired game publisher | Attached glasses; model/firmware must be identified |

Fedora supplies game state. It does not enumerate remote-client glasses. Standalone Prism and native Moonlight combined presentation are distinct experiences sharing a layout contract.

Record ordinary display output, simultaneous Moonlight use, whole-surface pinning and independent spatial panels separately. Whole-surface pinning already provided by a device does not prove independent panel tracking.

Receiving-device details are still needed: glasses model, Beam versus Beam Pro, neckband model, phone operating system and iPad model.

Primary references: [XREAL SDK](https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK), [VITURE SDK](https://www.viture.com/developer/glasses-sdk/glasses), [Android external presentation](https://developer.android.com/reference/android/app/Presentation), [Apple external displays](https://developer.apple.com/documentation/uikit/presenting-content-on-a-connected-display).
