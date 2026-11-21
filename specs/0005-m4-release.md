# Spec 0005: M4 Polish and public release

Status: Reviewed. Closes issue #7.

## Goal

DegaussNG gets a proper English README, the version 1.0.0, and a release workflow that turns a tag
`v*` into a GitHub release with one self-contained `DegaussNG.exe`. Users download a ZIP, unpack it and
run the exe without installing .NET.

## Files

Create these files:

```
.github/workflows/release.yml
tests/DegaussNG.Tests/VersionTests.cs
```

Change these files:

```
README.md                                      complete rewrite
Directory.Build.props                          version and product metadata
src/DegaussNG/TrayController.cs                tooltip with version
```

`docs/media/boot-screen.png` is added separately and is only referenced by the README. Do not create it.

## Version

`Directory.Build.props` gets, in its existing `PropertyGroup`:

```xml
<Version>1.0.0</Version>
<Product>DegaussNG</Product>
<Authors>Markus Kremer</Authors>
<Copyright>Copyright (c) 2026 Markus Kremer</Copyright>
```

The tray tooltip reads `DegaussNG 1.0.0`, built from the version of the running assembly as
`Major.Minor.Build`.

## Release workflow

`.github/workflows/release.yml`, named `Release`:

- Triggers on `push` of tags matching `v*`. Permission `contents: write`.
- One job on `windows-latest`, every `run` step with `shell: pwsh`.
- Steps: checkout (`actions/checkout@v4`), setup .NET 8 (`actions/setup-dotnet@v4`, `8.0.x`),
  `dotnet test -c Release`, then publish, package and release as below.
- The version is the tag name without the leading `v`.
- Publish:
  `dotnet publish src/DegaussNG/DegaussNG.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:Version=<version> -o publish`
- Package: `DegaussNG-<version>-win-x64.zip` with `publish/DegaussNG.exe`, `README.md` and `LICENSE` at the
  top level, plus `DegaussNG-<version>-win-x64.zip.sha256` holding the lowercase SHA-256 hex of the ZIP,
  two spaces and the ZIP file name.
- Release: `gh release create <tag> <zip> <sha256 file> --title "DegaussNG <version>" --generate-notes`
  with `GH_TOKEN` set to `${{ github.token }}`.
- Every step fails the job on a non-zero exit code.

## README

English, Markdown, in this order with exactly these level-2 headings:

1. `# DegaussNG`, one or two sentences of pitch, then the GIF placeholder: the HTML comment
   `<!-- demo GIF: docs/media/degauss.gif -->` and the line `*Demo GIF coming soon.*`
2. `## Features`: the degauss effect with hotkey, sound and reduced motion; the modem dial-up sound on a real
   network connection with short and full mode; the fake BIOS boot screen at sign-in. Mention that every
   sound and picture is synthesized by the app itself, without samples.
3. `## Screenshots`: the image `docs/media/boot-screen.png` with the alt text `Fake BIOS boot screen`.
4. `## Install`: download the ZIP from `https://github.com/MKRWW/DegaussNG/releases`, unpack, run
   `DegaussNG.exe`, no installation and no .NET needed. Windows 10 or 11, 64 bit. The exe is not signed, so
   SmartScreen may ask: `More info`, then `Run anyway`. Exit through the tray menu.
5. `## Usage`: a table of every tray menu item in menu order with one short sentence each, taken from
   `TrayController.cs`. The default hotkey `Ctrl+Alt+D`. A click on the tray icon stops the modem sound, any key
   or click ends the boot screen.
6. `## When the modem sound plays`: only after the network was gone for at least 8 seconds and is then stable
   for 4 seconds, at most once a minute, never at app start, and virtual adapters such as WSL, Hyper-V, Docker
   and VPNs do not count.
7. `## Settings`: the file `%APPDATA%\DegaussNG\config.json` with every field and its default, taken from
   `AppConfig.cs`, the log `%LOCALAPPDATA%\DegaussNG\degauss.log`, and that `Start with Windows` writes the value
   `DegaussNG` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` only when switched on.
8. `## Command line`: `--autostart`, `--boot <log>`, `--netstate <log>` and `--selftest <cycles> <log>`, one
   sentence each, taken from `App.xaml.cs`.
9. `## Build from source`: .NET 8 SDK, `dotnet build -c Release`, `dotnet test -c Release`, and
   `dotnet run --project tools/SoundSynth -- assets/sounds` to regenerate the sounds byte for byte.
10. `## Privacy`: no network requests, no telemetry; the app only reads the local adapter state.
11. `## License`: MIT, link to `LICENSE`. All sounds and graphics are original; no trademarks or third-party
    samples are used.

Tone: friendly and a little nostalgic, not kitschy. No emojis.

## Acceptance criteria

- AC1: Given a clean checkout, `dotnet build -c Release` succeeds with 0 warnings and all tests pass.
- AC2: A test asserts that the `DegaussNG.Core` assembly has the version 1.0.0.0.
- AC3: The README has exactly the eleven sections above in that order, the GIF placeholder, the image link
  `docs/media/boot-screen.png`, the releases link and a link to `LICENSE`.
- AC4: Every tray item, config field and command line option in the README exists in the code, and nothing
  the code offers in those three places is missing.
- AC5: `release.yml` is valid YAML, triggers only on tags `v*`, and its publish command, run locally with
  `-p:Version=1.0.0`, produces a single `DegaussNG.exe` that starts and passes `--boot` (gate, measured).
- AC6: No versioned file mentions tools, assistants or automation used to write it.

## Do NOT

- No change to behaviour, sounds, WAV files or tests other than the tooltip and the new version test.
- No new NuGet packages. No signing. No files outside the Files section.
- Do not use web search. Do not run git commands.

## Abschluss

Run these commands in this order and print their output verbatim:

1. `dotnet build -c Release`
2. `dotnet test -c Release --no-build`
