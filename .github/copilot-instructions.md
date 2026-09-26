# Repository instructions

These instructions apply across the repository. C# coding conventions below
apply to application and test code under `src/`.

## C# solution conventions

Keep all application and test projects as sibling folders under `src`, alongside
`src/RcCar.sln`. Update solution entries and project references when moving files.

Use one class, interface, enum, or substantial record per file, with the filename
matching the type. Small, closely related data records may share a clearly named
file, such as `BleModels.cs`. Document each grouped record's purpose and units.

Always use curly braces for `if`, `else`, loops, `using` statements, and `lock`,
even for a single statement. Put braces on separate lines (Allman style).
Keep one statement per line. Simple expression-bodied members and auto-properties
are fine; expand accessors and methods when they contain several operations.

Optimize for readability. Use descriptive names, blank lines between logical
steps, and line breaks for long conditions and argument lists. Keep related
assignments together. Prefer straightforward control flow over dense expressions
or unnecessary abstractions. Follow `src/.editorconfig`.

Explain the reasons behind non-obvious behavior. For BLE code, describe packet
offsets, bit flags, units, callback lifetimes, cancellation, and publisher shutdown
ordering. Distinguish behavior established by protocol evidence from unknown
fields; do not invent meanings for fixed bytes or reply validation rules.

The car uses BLE manufacturer-data advertisements for pairing and control, with
no GATT connection. Keep packet encoding in `RcCar.Core`, WinRT calls in
`RcCar.Bluetooth.Windows`, and WPF presentation and composition in `RcCar.App`.
Core must remain independent of Windows and the UI.

Preserve one active workflow and one publisher at a time, fresh pairing before
motion, input expiry/focus-loss neutralization, and final neutral cleanup after
cancellation or failure. Wait for publisher shutdown before sending another
state. API transmission success does not establish physical movement.

Validate code changes from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\build.ps1 -Test
```

Tests belong in
`src/RcCar.Core.Tests` and use fake transports without BLE traffic. Add focused
tests for changed behavior; formatting-only changes need the existing suite.
