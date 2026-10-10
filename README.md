# Volume Mixer Plugin for Macro Deck 3

A **Macro Deck 3** plugin that provides Windows audio control directly from your macro pad. Monitor
and control audio devices, per-application volumes, and default device selection, a lightweight
alternative to Voicemeeter Banana for basic audio routing needs.

This is the Macro Deck 3 port of
[MacroDeckVolumeMixerPlugin](https://github.com/DiegoFdezAb/MacroDeckVolumeMixerPlugin) (Macro Deck 2).
The behaviour is the same; the plugin model is not. See
[What changed from Macro Deck 2](#what-changed-from-macro-deck-2).

Windows only (Windows Core Audio API). `win-x64`, framework-dependent on .NET 10.

---

## Features

### Current

- **Default Audio Device Info**: See which playback device is currently set as default (Multimedia and
  Communications roles)
- **Default Microphone Info**: See which input device is currently set as default
- **Per-Application Volume Control**: View and adjust volume for individual apps (Spotify, Discord,
  games, etc.)
- **Volume Sliders**: Bind a slider widget straight to an application's volume, in both orientations
- **Mute/Unmute Apps**: Toggle mute state for specific applications
- **Switch Default Output**: Change the default playback device with a button press
- **Switch Default Microphone**: Change the default input device with a button press
- **Switch Output + Microphone**: Change both output and input devices together (profile switching)
- **Device Switching**: Switch output and/or microphone devices via actions
- **Toggle Mode**: Toggle between 2 devices with a single button press (output, microphone, or both)

---

## Requirements

- **[Macro Deck 3](https://macro-deck.app)** (`>=3.0.0-0`)
- **Windows 10/11** (uses Windows Core Audio API)
- **.NET 10 Runtime** (shipped with Macro Deck)

---

## Installation

### From the plugin artifact

1. Download the `.macroDeckPlugin` artifact from the releases.
2. Import it in Macro Deck on your PC.

### Manual build

```bash
git clone https://github.com/DiegoFdezAb/MacroDeck3VolumeMixerPlugin
cd MacroDeck3VolumeMixerPlugin

# Build, test, pack
dotnet build
dotnet test
macrodeck-plugin build --source src/VolumeMixerPlugin --output ./artifacts
macrodeck-plugin validate --artifact ./artifacts/com.diegofdezab.volume-mixer-<version>.macroDeckPlugin
```

An installed plugin lives under `%appdata%\MacroDeck\plugins\com.diegofdezab.volume-mixer\versions\<version>\`,
with a `manifest.json`, a `current.json` pointing at the active version, and one `runtimes\<rid>\`
directory per declared platform. Do not copy a loose DLL into that folder: the host verifies the
`files[]` digests in the manifest.

---

## Usage

### Variables (exposed to Macro Deck)

| Variable Name | Type | Description |
|---------------|------|-------------|
| `volumemixer_default_device` | String | Name of the current default multimedia output device |
| `volumemixer_comm_device` | String | Name of the current default communications output device |
| `volumemixer_default_mic` | String | Name of the current default multimedia input device |
| `volumemixer_comm_mic` | String | Name of the current default communications input device |
| `volumemixer_app_<name>_volume` | Numeric | Volume level (0-100) for a tracked app, **writable** |
| `volumemixer_app_<name>_muted` | Boolean | Mute state for a tracked app, read only |

The four device variables are polled every 2 seconds. Example button label:
`{volumemixer_default_device}`.

Per-application variables come from the variable catalog: pick an app that is producing audio and bind
`volumemixer_app_<name>_volume` or `volumemixer_app_<name>_muted`. The `<name>` part is the process
name lowercased, with every run of characters outside `a-z0-9_` replaced by `_`, and a leading
non-letter prefixed with `app_`. Example: `Spotify: {volumemixer_app_spotify_volume}%`.

### Sliders

`volumemixer_app_<name>_volume` is declared writable, so a slider widget binds to it directly and
writes the application volume as you drag. The value is clamped to 0-100 before it reaches the audio
session. Both slider orientations work; Macro Deck sends the adjustment, not a press, so the plugin
never sees a button.

`volumemixer_app_<name>_muted` stays read only. Use the `toggle-mute` action for it.

### Actions

| Action | Id | Configuration |
|--------|----|---------------|
| **Set Default Output** | `set-default-output` | Device name or ID. Toggle mode: select 2 devices to switch between them |
| **Set Default Microphone** | `set-default-mic` | Device name or ID. Toggle mode: select 2 microphones to switch between them |
| **Set Default Output + Microphone** | `set-default-pair` | Output device, Input device. Each can optionally use toggle mode |
| **Set App Volume** | `set-app-volume` | App name, Volume (0-100) |
| **Volume Up/Down** | `volume-up` / `volume-down` | App name. Adjusts by 5% |
| **Mute App** | `toggle-mute` | App name |
| **Refresh Devices** | `refresh-devices` | None |

Device pickers are dynamic: they list the currently active endpoints. A device unplugged between
configuration and press fails the action truthfully instead of switching blindly.

### Toggle Mode

All device switching actions support a **Toggle Mode** that switches between 2 devices with a single
button press:

1. Select Device 1 and Device 2 in the action configuration
2. Each press switches to the device that is not currently default
3. The plugin detects the current device itself

**Note:** The two devices must be different. If a device becomes unavailable (disconnected), the
toggle will not execute and a warning is logged.

### Example button configurations

**Show current default device:**

- Label: `{volumemixer_default_device}`
- No action needed, the variable updates automatically

**Spotify volume:**

- Label: `Spotify: {volumemixer_app_spotify_volume}%`
- Slider bound to `volumemixer_app_spotify_volume`
- Long press action: Volume Down (App: Spotify)

---

## What changed from Macro Deck 2

The behaviour of every action and variable is preserved. The packaging and the contract are not.

| | Macro Deck 2 | Macro Deck 3 |
|---|---|---|
| Plugin model | In-process extension | Out-of-process plugin, its own .NET process |
| Artifact | Loose DLL copied to `%appdata%\Macro Deck\plugins\` | `.macroDeckPlugin` package with a signed manifest |
| Manifest | `ExtensionManifest.json` | `manifest.json`, schema validated, digests recomputed on pack |
| Runtime | .NET 8 | .NET 10, shipped with the host |
| Entry point | `net8.0-windows` DLL | `runtimes/win-x64/`, framework-dependent |
| Identity | Extension id | `com.diegofdezab.volume-mixer`, publisher DiegoFdezAb |
| Strings | Inline literals | `Localization/Strings.resx`, English shipped, more languages fall back to it |
| Install | Manual copy or MD2 Extension Store | Artifact import, MD3 Store submission |
| Verification | Manual | `macrodeck-plugin test`, conformance report required before publishing |

Two things are new rather than ported:

- **Writable volume variables**, so a slider can drive an application's volume
- **Dynamic device pickers** resolved from the live endpoint list at configuration time

---

## Screenshot

| Screenshot | What it shows |
|---|---|
| `docs/screenshots/set-default-output-toggle.png` | An action button whose label is the `{volumemixer_default_device}` variable, running the `set-default-output` action in toggle mode between two devices |

---

## Technical details

### Audio API

This plugin uses **[NAudio](https://github.com/naudio/NAudio)** with the Windows **Core Audio API**
(WASAPI) to:

- Enumerate audio endpoints (`MMDeviceEnumerator`)
- Access audio sessions per device (`AudioSessionManager`)
- Control per-application volume (`SimpleAudioVolume`)

### Memory management

The plugin disposes its COM objects so it does not leak audio sessions:

- Single `MMDeviceEnumerator` instance, disposed on shutdown
- Device cache with a 15 second lifetime
- Sessions released after each snapshot

### Limitations

- **Windows only**: Core Audio API is not available on Linux or macOS
- **No audio routing**: cannot route audio between devices like Voicemeeter
- **Process-based detection**: apps are identified by process name, not window title

---

## Troubleshooting

### Plugin not loading

- Check Macro Deck is `>=3.0.0-0`
- Check the plugin folder holds a version directory with a `manifest.json`
- Read the logs at `%appdata%\MacroDeck\logs\`

### Apps not showing

- The app must be actively producing audio to appear
- Some system sounds show as empty or unnamed sessions
- Store apps may show under a different process name

### Volume changes not applying

- Ensure the app is not locked by another volume controller
- Some apps ignore external volume changes (rare)

### A slider moves but the volume does not

- The variable must be the `_volume` one, the `_muted` one is read only
- The app must have an audio session at that moment, otherwise the write reports unavailable

---

## Contributing

Contributions welcome. Please:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

For bugs or feature requests, open an issue.

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

## Credits

- **[NAudio](https://github.com/naudio/NAudio)**: audio library for .NET
- **[Macro Deck](https://macro-deck.app/)**: the macro pad software
- **[MacroDeckVolumeMixerPlugin](https://github.com/DiegoFdezAb/MacroDeckVolumeMixerPlugin)**: the
  Macro Deck 2 original this is ported from
- Inspired by the need for simple audio control without Voicemeeter overhead
