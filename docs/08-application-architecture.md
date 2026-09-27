# 08 — From experiments to a desktop toolkit

[Start here](01-start-here.md) · [Previous: static analysis](07-static-analysis-walkthrough.md)

After the original keyboard controller worked, the project moved into three
projects under `src`. The owner requested readable C#, SOLID principles, and
interfaces at the boundaries. The goal was a desktop exploration tool; OpenCV
and Python integration were left for a later version.

## Responsibilities and dependencies

| Project                   | Owns                                                                                                                                | Does not own |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | --- |
| `RcCar.Core`              | Three-byte identities, validated driving state, gears, packet encoding, reply parsing, pairing, diagnostics, and continuous control | Windows APIs, keyboard events, windows, or file paths |
| `RcCar.Bluetooth.Windows` | Adapter inspection, passive receiving, advertisement publishing, status/error handling, and confirmed radio shutdown                | Vehicle selection, packet semantics, diagnostic sequence, or UI |
| `RcCar.App`               | WPF presentation, view model, composition, key sampling, and session files                                                          | Duplicated protocol encoding or direct radio calls from buttons |

The Windows project implements interfaces defined in Core. The app constructs
the concrete implementations at startup. There is no dependency from Core to
Windows or WPF, and no dependency from the Windows DLL to the application.

This uses domain types where they clarify meaning rather than adding a large
framework. `CarIdentity` is the three-byte value returned in the protocol;
`DriveState` describes throttle, steering, and speed; `CarCandidate` also records
the observed radio address and signal strength. Neither addresses nor vehicle
identities are claimed to be authenticated or universally unique.

## Interfaces with specific purposes

`IAdapterInspector`, `IAdvertisementReceiver`, and `IAdvertisementTransmitter`
separate read-only inspection, incoming advertisements, and outgoing radio
windows. They are independently replaceable in tests. `ICarService` exposes
discovery, diagnostic tests, and ongoing driving. `IControlInput` supplies the
latest requested state without knowing where the input came from.

`ISessionEvents` separates workflow reporting from storage and display. The app
writes those events to a detailed file and a separate summary. A user-entered
physical observation is a distinct event, not inferred from a successful Windows
publisher status.

## One workflow owns the radio

Discovery advertises the known request while collecting matching replies over
a bounded window. It reports arbitrary returned identities, not just the
original car. A single candidate is selected automatically; several require
selection. Duplicate identities at different addresses are treated as ambiguous.

Before a diagnostic or driving session, Core requires a fresh matching reply
for the selected identity. It then sends initial neutral and performs the
requested sequence. A semaphore rejects competing workflows rather than letting
discovery and driving accidentally publish over one another.

During keyboard driving, changed state replaces the active advertisement;
unchanged state refreshes roughly every 600 ms with an advancing counter. A
publisher must finish its stop operation before a replacement begins. If Windows
cannot confirm its stop, the transport refuses new publishers until the app is
restarted. Radio startup and shutdown waits are bounded.

Cancellation after successful pairing attempts five final neutral states using
a cleanup token independent of the cancelled input session. Cleanup failures
remain errors and are shown to the user. The code cannot guarantee delivery if
the adapter fails or the process is terminated.

## Keyboard input and presentation

Driving is explicitly enabled. Arrow keys elsewhere in the app do not request
movement. While driving, release neutralizes the appropriate axis; opposing
arrows cancel; Space advances one gear per press; Enter toggles the tested
byte-10 light flag once per press. The UI displays requested state, not vehicle
telemetry.

Focus loss clears movement and requires release before resuming. Core also
expires an input heartbeat older than 250 ms. This protects against a stalled
UI while the radio workflow continues, not a frozen process. The keyboard and
focus APIs stay in the presentation project; the input-state rules can be tested
without a window.

The WPF view contains layout and bindings. Its code-behind handles actual window
and keyboard events. The view model coordinates commands and application state;
it does not construct packets. Physical observations and the log folder are
available in the same window as discovery, tests, and driving.

## Validation and what remains

The xUnit suite checks packet vectors, all gears/counters, malformed replies,
arbitrary learned identities, input release, stale input, fresh-target pairing,
diagnostic ordering, cancellation, cleanup failure, and competing workflows.
Fake transmitters make assertions about what would be sent without touching
Bluetooth. An offline WPF render/close test checks application startup and
shutdown without radio use.

These checks do not replace a physical run of the refactored application.

A future integration can supply another `IControlInput` or another executable
using `ICarService`. That leaves room for a Python-facing process or OpenCV
tracking without adding that complexity to this version.
