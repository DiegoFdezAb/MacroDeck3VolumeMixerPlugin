# Volume Mixer for Macro Deck 3

Windows volume mixer plugin for [Macro Deck 3](https://macro-deck.app): see the default output
and microphone on buttons, and switch between devices with one press. Port of
[MacroDeckVolumeMixerPlugin](https://github.com/DiegoFdezAb/MacroDeckVolumeMixerPlugin) (Macro Deck 2)
to the Macro Deck 3 out-of-process plugin model.

Windows only (Windows Core Audio API). `win-x64`, framework-dependent on .NET 10.

## Actions

| Action | Id | What it does |
|---|---|---|
| Set default output | `set-default-output` | Switches the default playback device. Set a second device to toggle between the two |
| Set default microphone | `set-default-mic` | Switches the default recording device, with the same optional toggle |
| Set output and microphone | `set-default-pair` | Switches an output + microphone profile in one press |
| Refresh audio devices | `refresh-devices` | Clears the cached device lists |

Device pickers are dynamic: they list the currently active endpoints. A device unplugged
between configuration and press fails the action truthfully instead of switching blindly.

## Variables

| Variable | Content |
|---|---|
| `volumemixer_default_device` | Default multimedia playback device name |
| `volumemixer_comm_device` | Default communications playback device name |
| `volumemixer_default_mic` | Default recording device name |
| `volumemixer_comm_mic` | Default communications recording device name |

Polled every 2 seconds. Example button label: `{volumemixer_default_device}`.

## Requirements

- .NET SDK 10.0
- Macro Deck 3 (`>=3.0.0-0`)
- Windows 10/11

## Build and test

```bash
dotnet build
dotnet test
macrodeck-plugin run --project src/VolumeMixerPlugin --stub-host
macrodeck-plugin test --project src/VolumeMixerPlugin
macrodeck-plugin build --source src/VolumeMixerPlugin --output ./artifacts
macrodeck-plugin validate --artifact ./artifacts/com.diegofdezab.volume-mixer-1.0.0.macroDeckPlugin
```

## Install

Use the `.macroDeckPlugin` artifact from the releases, or build it with the commands above.
Per-application volume control from v2 is planned for a later release.

## License

MIT. Audio via [NAudio](https://github.com/naudio/NAudio).
