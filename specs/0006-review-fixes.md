# Spec 0006: Fixes from the code review before the release

Status: Reviewed. Part of issue #7.

## Goal

Six defects found in a review of the whole code base are fixed before the first public release.

## Files

Change only these files:

```
src/DegaussNG/HotkeyService.cs
src/DegaussNG/TrayController.cs
src/DegaussNG.Core/ConnectionDebouncer.cs
src/DegaussNG/NetworkWatcher.cs
src/DegaussNG/BootRunner.cs
.github/workflows/release.yml
tests/DegaussNG.Tests/ConnectionDebouncerTests.cs
tests/DegaussNG.Tests/VersionTests.cs
```

## F1: A failed hotkey change keeps the old hotkey

Today `HotkeyService.Apply` unregisters the working hotkey before it tries the new one, and
`TrayController.OnHotkeyClick` saves the new gesture before it knows whether it works. A gesture held by
another program leaves the app without any hotkey, also after a restart.

- `HotkeyService.Apply(AppConfig)`: when the new gesture cannot be registered and a gesture was registered
  before, register the previous gesture again and return false. `_current` and `_registered` describe the
  hotkey that is really active afterwards.
- `TrayController.OnHotkeyClick`: build the candidate config with the new gesture and call
  `_hotkey.Apply(candidate)`. Only on success assign `_config`, save it and update the runners. On failure
  keep `_config` unchanged and show the balloon
  `Hotkey <new> could not be registered. <old> stays active.` with `ToolTipIcon.Warning`.

## F2: A failed save no longer leaves the runners behind

`TrayController.Update` assigns `_config`, updates `_runner` and `_boot`, and only then saves. A save
exception is caught and logged with `Log.Error("config could not be saved: " + message)`. The same applies
to the save in `OnHotkeyClick`.

## F3: A reconnect after sleep plays the sound

Add to `ConnectionDebouncer`:

```csharp
public void Suspend(TimeSpan now);
```

- `Suspend` sets the confirmed state to offline, the last reported state to offline and the change time to
  `now`. A following online report then fires after `stableOnline`, as for any real connection, and the
  cooldown still applies.
- `Report(true, now)` while the last report is offline and the state is confirmed online: when
  `now - changeTime` has reached `minimumOffline`, the confirmed state first becomes offline. A long drop
  therefore counts even when no `Poll` happened during it.

`NetworkWatcher` subscribes to `Microsoft.Win32.SystemEvents.PowerModeChanged` in `Start`. On
`PowerModes.Suspend` it calls `Suspend` with the watcher clock through `Dispatcher.BeginInvoke`. `Dispose`
unsubscribes, because the event is static.

## F4: The Start with Windows check shows the truth

After `Autostart.Enable()` or `Autostart.Disable()`, the click handler sets
`_autostartItem.Checked = Autostart.IsEnabled()`.

## F5: The boot result is the one the run ended with

`BootRunner.RunAsync` returns the result that `Finish` completed the task with: `return await _closed.Task;`.
The catch path stays as it is.

## F6: One version source

- `VersionTests`: instead of the fixed 1.0.0.0, the test finds `Directory.Build.props` by walking up from
  `AppContext.BaseDirectory`, reads the `Version` element and asserts that the `DegaussNG.Core` assembly has
  that version with a revision of 0.
- `release.yml`: a new step after the version step reads `Version` from `Directory.Build.props` and fails the
  job with a clear message when it differs from the tag version.

## Acceptance criteria

- AC1: `dotnet build -c Release` succeeds with 0 warnings and all tests pass.
- AC2: Connected, offline reported at 10, no `Poll` until online is reported at 20, then polls every second:
  fires exactly once at 24.
- AC3: Connected, `Suspend` at 10, online reported at 11, polls every second: fires exactly once at 15.
- AC4: Connected, a firing at 5 after a reconnect, `Suspend` at 20, online at 21: silent because of the
  cooldown. All existing debouncer tests still pass unchanged.
- AC5: The version test passes with the current props and would fail if the props said 1.0.1.
- AC6 (gate, measured): with `Ctrl+Alt+F9` held by another process, `Apply` with `Ctrl+Alt+F9` after
  `Ctrl+Alt+D` returns false and `Ctrl+Alt+D` is still registered by the app.

## Do NOT

- No other behaviour changes. No new files, packages or P/Invoke declarations.
- No mention of tools, assistants or automation in code comments or documents.
- Do not use web search. Do not run git commands.

## Abschluss

Run these commands in this order and print their output verbatim:

1. `dotnet build -c Release`
2. `dotnet test -c Release --no-build`
