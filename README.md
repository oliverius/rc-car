# RC Car Lab

A Windows desktop toolkit for exploring a small toy car's BLE advertising protocol.
The original experiments produced working forward/reverse movement, steering,
and keyboard control. The new application brings those workflows into one WPF
window and discovers vehicle identities instead of assuming the original car.

Read the investigation from [01 — Start here](docs/01-start-here.md).

## Build and run

Requires Windows 10 build 19041 or newer, a compatible Bluetooth adapter, and
the .NET 10 SDK to build. Running the desktop app requires the .NET 10 Windows
Desktop runtime. The build script uses the Windows-installed `dotnet` on PATH.
First restore needs access to NuGet.
`global.json` selects the latest installed stable .NET 10 SDK feature band.
Build and launch scripts live in `build/`. `global.json` and
`Directory.Build.props` stay at the repository root so the CLI and IDE discover
the SDK selection and shared compiler settings automatically.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\build.ps1 -Test
.\build\run.cmd
```

In VS Code with C# Dev Kit, load `src/RcCar.sln` in Solution Explorer, then
right-click `RcCar.App` and select **Debug > Start New Instance**. Install the
[.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and restart
VS Code if the extension cannot find a compatible SDK.

Alternatively, open [src/RcCar.sln](src/RcCar.sln) in Visual Studio 2026 with the **.NET desktop
development** workload and select `RcCar.App` as the startup project.
Visual Studio is optional; the SDK is sufficient for the scripts above.

## Use the desktop app

1. **Check adapter.** This is read-only; it does not transmit. Capabilities do
   not guarantee publishing works, so discovery also tests actual radio use.
2. **Discover cars.** Keep the car and handheld remote off; start discovery,
   then turn the car on at **PUBLISHER READY**. The ten-second window collects
   matching reply identities and addresses. Select the intended car if several
   appear. Only power on the intended test car when possible.
3. **Run diagnostics.** With wheels clear and the power switch reachable, turn
   the car off before each test. Click forward, steering, or reverse; turn it
   on at **PUBLISHER READY**. Each test freshly pairs with the selected identity.
   Forward/reverse use one 0.3-second pulse at speed 20. Record the observed
   wheel/light behaviour using **Save physical observation**.
4. **Drive.** Turn the car off, click **Enable keyboard driving**, then switch
   it on at **PUBLISHER READY**. Wait for keyboard readiness and release all
   arrows before starting. Hold arrows to move/steer, release to neutral, and
   press Space to cycle 40%, 70%, 100%. Escape or **Stop session** ends the
   session after neutral cleanup. Closing the window also waits for cleanup.

Keyboard control requires the window to be active. Losing focus sends neutral
and requires release before resuming. Steady neutral refreshes continue while
idle; the lights may flash again after the session ends. If movement persists
or neutral fails, use the car's power switch. A process crash or radio failure
can prevent cleanup, and the firmware's loss-of-signal motor behaviour is not
established.

This is a toolkit for the decoded **standard-Car** protocol, not every toy sold
with similar bodywork. The original vehicle's identity is not hard-coded in
the new app. A three-byte identity is not authentication or guaranteed globally
unique. Discovery showing the same identity at multiple addresses disables
movement for that selection until the ambiguity is resolved.

## Source structure

| Project                       | Responsibility |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `src/RcCar.Core`              | Platform-independent identity/state models, packet codec, input lease/gears, and exclusive pairing/control workflows |
| `src/RcCar.Bluetooth.Windows` | Implements Core's adapter, transmitter, and receiver interfaces using WinRT                                          |
| `src/RcCar.App`               | WPF views, view model, composition, physical keyboard input, and session logging                                     |
| `src/RcCar.Core.Tests`        | xUnit protocol, input, pairing, cancellation, failure, and radio-ownership tests using fake transports               |

Core has no Windows or UI dependency. The Windows DLL references Core; the app
composes both through interfaces. A future command-line or Python-facing bridge
can reuse those DLLs without depending on the WPF window.

## Logs and validation

Each app launch creates a folder under `%LOCALAPPDATA%\RcCar\sessions`.
**Open session folder** shows its detailed `events.jsonl` and readable
`summary.md`. Logs include adapter/car identifiers and packet payloads.
Observations are recorded separately from API success. The UI retains the most
recent 500 events; the files retain the complete session.

Automated tests use fake radios and send no BLE traffic. An optional offline
UI check, `build\run.cmd --smoke-test`, renders the WPF window to the OS temporary
folder as `RcCar-smoke.png` and closes it without reading the adapter or using
Bluetooth. The refactored app still needs a physical validation run; successful
prototype experiments are not a substitute for testing the new application.

See [08 — Application architecture](docs/08-application-architecture.md) for the
interfaces, lifecycle, and extension points.
