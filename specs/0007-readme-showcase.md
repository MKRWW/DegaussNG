# Spec 0007: README showcase before going public

Status: Reviewed. Closes issue #9.

## Goal

The README becomes a project page that makes people want to try DegaussNG within ten seconds: a logo, badges,
a real demo GIF, screenshots side by side, a three-step quick start and an FAQ. All facts stay correct.

## Files

```
README.md                    complete rewrite
docs/media/logo.svg          new, hand-written SVG
```

These media files already exist and are only referenced: `docs/media/degauss.gif` (720x405 demo of the
effect over a test pattern), `docs/media/degauss-effect.png` (1280x720 still at the effect peak) and
`docs/media/boot-screen.png` (1280x800 boot screen). Do not create or change them.

## Logo (`docs/media/logo.svg`)

A 128 by 128 `viewBox`, no external references, no fonts, no text. A small CRT monitor in the style of the
tray icon:

- Case: rounded rectangle `x=10 y=16 width=108 height=78 rx=10`, fill `#464A50`, stroke `#282C30` width 3.
- Screen: rounded rectangle `x=20 y=26 width=88 height=58 rx=8`, fill `#A8C4D6`.
- A wobbling beam across the screen: one `path` from `x=26` to `x=102` around `y=55`, a sine-like curve whose
  swing decays from 12 to 2 to the right, stroke `#E8F4FC` width 4, round caps, no fill.
- Two thin colour fringes along the same curve, offset by 2 to the left in `#FF4D6D` and 2 to the right in
  `#3DDCFF`, width 2, opacity 0.7, drawn below the beam.
- Neck `x=52 y=94 width=24 height=10` and foot `x=34 y=104 width=60 height=10 rx=4`, both `#464A50`.

## README

English, GitHub Markdown with the small amount of HTML GitHub allows. No emojis. Friendly, a little
nostalgic, never kitschy. Every fact must match the code, as in the current README.

In this order:

1. A centered header (`<p align="center">`): the logo at width 120, the title `DegaussNG` as `<h1>`, the bold
   tagline `Your flat screen, now with 1998 included.`, then a line of badges, then a line of links
   `Download · Features · Quick start · FAQ` that jump to the sections, with `Download` pointing to
   `https://github.com/MKRWW/DegaussNG/releases/latest`.
   Badges, each linked to the matching page:
   - CI: `https://github.com/MKRWW/DegaussNG/actions/workflows/ci.yml/badge.svg`
   - Release: `https://img.shields.io/github/v/release/MKRWW/DegaussNG`
   - License: `https://img.shields.io/github/license/MKRWW/DegaussNG`
   - Platform: `https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4`
   - .NET: `https://img.shields.io/badge/.NET-8-512BD4`
2. The demo GIF centered at width 720 with the alt text `The degauss effect over a test pattern`, and below it
   a one-line italic caption.
3. A short intro of two or three sentences: the hum, the thump and the wobble of a CRT, the screech of a
   modem, the BIOS counting memory, and that this app brings them back without any of the hardware.
4. `## Features`: an HTML table with three columns `Degauss`, `Dial-up` and `Boot screen`. Each cell has two or
   three sentences. Below it a two-column HTML table with `degauss-effect.png` and `boot-screen.png` at
   width 100 percent of their cell, each with a short caption. Then one sentence that every sound and picture
   is synthesized by the app itself.
5. `## Quick start`: three numbered steps: download the ZIP from the latest release, unpack and run
   `DegaussNG.exe`, press `Ctrl+Alt+D`. Then one note on SmartScreen (`More info`, then `Run anyway`) and that
   no installation and no .NET are needed.
6. `## Using it`: the tray menu table as in the current README, and the three interaction hints (hotkey, click
   on the tray icon stops the modem sound, any key or click ends the boot screen).
7. `## When does the modem sound play?`: the rules from the current README as a short bullet list, plus that a
   reconnect after sleep counts.
8. `## Settings and files`: a `<details>` block with the summary `Where DegaussNG keeps its settings`, holding
   the settings table, the log file and the autostart registry value, as in the current README.
9. `## Command line`: a `<details>` block with the summary `Options for scripts and testing`, holding a table of
   the four options from the current README.
10. `## FAQ`, each question as a bold line followed by the answer:
    - Does this really degauss my monitor? No. Flat panels have no shadow mask to magnetize; the effect is a
      picture and a sound.
    - Can it harm my screen? No. It only draws a short animation on top of the desktop.
    - Why is the download so big? It bundles the .NET runtime, so nothing has to be installed.
    - Windows warns me about the file. The exe is not signed; the source is all here.
    - How do I make it quieter or silent? The `Volume` menu, `Modem sound` and `Boot screen at startup`.
    - How do I uninstall it? Exit it, switch off `Start with Windows` first if it was on, delete the exe and
      the folders `%APPDATA%\DegaussNG` and `%LOCALAPPDATA%\DegaussNG`.
11. `## Under the hood`: four short bullets: the overlay is a WPF pixel shader compiled at run time from HLSL
    over a screenshot of each monitor; playback through NAudio; every sound is rendered sample by sample by
    `tools/SoundSynth` and is byte-for-byte reproducible; the debounce, adapter and boot logic is covered by
    unit tests.
12. `## Build from source`: as in the current README.
13. `## Privacy`: as in the current README.
14. `## License`: MIT with the link to `LICENSE`, and that all sounds and graphics are original, with no
    trademarks or third-party samples.

## Acceptance criteria

- AC1: `docs/media/logo.svg` is well-formed XML, uses `viewBox="0 0 128 128"`, and has no `<text>`, `<image>` or
  external reference.
- AC2: The README contains, in this order, the header, the GIF, the intro and the sections 4 to 14 with exactly
  the headings above.
- AC3: Every relative link and image in the README points to an existing file, and the anchor links in the
  header reach existing headings.
- AC4: Every tray item, config field, default and command line option still matches the code.
- AC5: No emojis, and no mention of tools, assistants or automation used to write it.
- AC6: `dotnet build -c Release` and `dotnet test -c Release --no-build` stay green.

## Do NOT

- No code changes. No files outside the Files section.
- Do not use web search. Do not run git commands.

## Abschluss

Run these commands in this order and print their output verbatim:

1. `dotnet build -c Release`
2. `dotnet test -c Release --no-build`
