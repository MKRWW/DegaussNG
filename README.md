# DegaussNG

A small Windows tray app that brings back the sounds and rituals of the CRT and LAN-party era.
Press a hotkey and your perfectly flat, perfectly silent panel goes *bwoommm*.

<!-- demo GIF: docs/media/degauss.gif -->

*Demo GIF coming soon.*

## Features

The degauss effect: a hotkey pulls a live picture of every monitor through a CRT degauss, the
geometry warbles, the colours fall apart and a low thump runs through the speakers. The default
hotkey is `Ctrl+Alt+D`, and reduced motion keeps the same effect at about a third of its strength.

The modem sound: when a real network connection comes back, the dial-up handshake plays again, in
the short mode or in the full one.

The boot screen: at sign-in the machine pretends to be an old 486 again, with a memory counter that
only ever runs forward and `Press DEL to enter SETUP`.

Every sound and every picture is synthesized by the app itself, without samples or recordings.

## Screenshots

![Fake BIOS boot screen](docs/media/boot-screen.png)

## Install

Download the ZIP from [the releases page](https://github.com/MKRWW/DegaussNG/releases), unpack it
and run `DegaussNG.exe`. Nothing is installed and no .NET is needed. Windows 10 or 11, 64 bit.

The exe is not signed, so SmartScreen may warn you the first time: choose `More info`, then
`Run anyway`. The app lives in the tray; exit through the tray menu.

## Usage

The tray menu, in order:

| Menu item | What it does |
| --- | --- |
| Degauss now | Runs the effect on every monitor right away. |
| Modem test | Plays the modem sound in the selected mode, even when the modem sound is off. |
| Boot screen now | Shows the fake boot screen once, even when it is off at startup. |
| Degauss enabled | Switches the hotkey on or off; `Degauss now` still works when it is off. |
| Reduced motion | Makes the effect and the boot screen calmer. |
| Modem sound | Switches the dial-up sound on a new connection on or off. |
| Modem mode | Chooses between `Short` and `Full`. |
| Boot screen at startup | Switches the boot screen at sign-in on or off. |
| Start with Windows | Adds or removes the sign-in entry. |
| Volume | Chooses 10 %, 30 %, 50 %, 70 % or 100 %. |
| Change hotkey... | Sets the hotkey, `Ctrl+Alt+D` by default. |
| Exit | Closes the overlays and quits the app. |

A click on the tray icon stops the modem sound. Any key or any click ends the boot screen.

## When the modem sound plays

The sound is not played on every network change. It plays only after the network was gone for at
least 8 seconds and is then stable for 4 seconds, at most once a minute, and never at app start.
Virtual adapters such as WSL, Hyper-V, Docker and VPNs do not count.

## Settings

The settings live in `%APPDATA%\DegaussNG\config.json`:

| Field | Default |
| --- | --- |
| `DegaussEnabled` | `true` |
| `ReducedMotion` | `false` |
| `Volume` | `0.3` |
| `Hotkey` | `Ctrl+Alt+D` |
| `ModemEnabled` | `true` |
| `ModemMode` | `Short` |
| `BootEnabled` | `true` |

A corrupt file is moved to `config.json.bak` and the defaults are used. Problems are written to
`%LOCALAPPDATA%\DegaussNG\degauss.log`.

`Start with Windows` writes the value `DegaussNG` under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, and only when the item is switched on.

## Command line

`--autostart`: the argument the sign-in entry uses, and the boot screen plays once.

`--boot <log>`: runs the boot screen once without tray, hotkey or settings, and writes the report
line to `<log>`.

`--netstate <log>`: writes whether the machine is connected, and one line per network adapter, to
`<log>`.

`--selftest <cycles> <log>`: runs `<cycles>` degauss cycles without tray, hotkey, sound or settings
and writes the counts to `<log>`.

## Build from source

You need the .NET 8 SDK.

```
dotnet build -c Release
dotnet test -c Release
```

The sounds are generated, not stored as recordings. To regenerate them byte for byte:

```
dotnet run --project tools/SoundSynth -- assets/sounds
```

## Privacy

No network requests and no telemetry. The app only reads the state of the local network adapters.

## License

[MIT](LICENSE). All sounds and graphics are original; no trademarks or third-party samples are used.
