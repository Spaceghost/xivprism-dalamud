# Compatibility targets

This is a target matrix, not a tested-hardware list. All live device capabilities remain unverified.

| Receiving device | Game source | Intended output |
| --- | --- | --- |
| Desktop or laptop PC | Remote game publisher or local FFXIV | Its monitors and supported attached glasses |
| Handheld PC | Remote game publisher or local FFXIV | Its screen and supported attached glasses |
| Smartphone | Any paired game publisher | Phone screen and supported attached outputs |
| Tablet | Any paired game publisher | Tablet screen and supported external presentation display |
| Android wearable or streaming device | Any paired game publisher | Supported attached glasses and other displays |

The computer running FFXIV supplies game state. The receiving device handles display detection, including connected glasses. Standalone Prism and native Moonlight combined presentation are distinct experiences sharing a layout contract.

Record ordinary display output, simultaneous Moonlight use, whole-surface pinning and independent spatial panels separately. Whole-surface pinning already provided by a device does not prove independent panel tracking.

Compatibility testing must record the receiving device and display models, operating system, firmware and connection type. Support claims require verification of the relevant combination.

Primary references: [XREAL SDK](https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK), [VITURE SDK](https://www.viture.com/developer/glasses-sdk/glasses), [Android external presentation](https://developer.android.com/reference/android/app/Presentation), [Apple external displays](https://developer.apple.com/documentation/uikit/presenting-content-on-a-connected-display).
