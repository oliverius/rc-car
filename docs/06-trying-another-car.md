# 06 — Trying another car and continuing the work

[Previous: driving](05-from-packets-to-driving.md) · [Start here](01-start-here.md) · [Next: static-analysis walkthrough](07-static-analysis-walkthrough.md)

## Start with the identity of your own car

The repository contains a working experiment for one unit. Similar bodywork, a `TRASPED` remote, or “2.4 GHz” on a listing does not establish the same protocol. Record the exact listing, advertised app support, underside label, board revision if accessible, and app name or QR-code destination. These would help build the compatibility map that this investigation could not establish.

A car missing from a phone's standard Bluetooth menu or an nRF scan is not automatically incompatible. In this investigation, useful reception came after transmitting a specific pairing request. Conversely, an advertisement called `RCCar` is not proof that it uses this exact movement format.

**The historical movement experiments and prototype controller target vehicle identity `F5 71 CD`.** They require a fresh reply for that identity. The subsequent [desktop toolkit](../README.md) under `src` learns and selects returned identities, then requires a fresh selected-identity reply before movement. Start with its discovery workflow when testing another car. The new application's physical compatibility still needs validation; no second unit has been tested in this investigation.

## A useful order for reproducing the result

1. Record the car and app identifiers before making assumptions about variants.
2. Use the pairing diagnostic with the car initially off. Turn it on when the publisher is ready, following that experiment's instructions.
3. Inspect the matching packet and learned identity. A completed window with zero matches is a negative observation, not a conclusive hardware diagnosis.
4. If Windows publishes but nothing matches, an independent phone scan of the laptop's request can separate a transmit-content problem from the unresolved car side.
5. Only after identifying the car, adapt the target identity if needed and use bounded movement tests with the wheels clear and power switch accessible.
6. Confirm direction, release behaviour, and steering before trying sustained control or higher speeds.

Normal exit attempts repeated neutral packets. Closing the terminal abruptly, losing Bluetooth, or terminating the process can prevent that cleanup. The tests do not establish a dependable motor-stop timeout on signal loss; use the car's power switch if motion persists.

## A compact protocol reference

The following is a reading aid for logs, not a specification for every app vehicle family.

| Field                                  | Standard-Car advertisement path                                                                       |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Manufacturer/company ID                | `0000`, passed separately from application data                                                       |
| Application payload                    | 19 bytes                                                                                              |
| Byte 0                                 | Family/type `04`                                                                                      |
| Byte 1                                 | Pair request `06`, control state `07`, expected pair reply `0A`                                       |
| Bytes 2–4 in control state             | Vehicle identity learned from reply                                                                   |
| Bytes 5–7                              | Client identity; this project uses `61 62 63`                                                         |
| Byte 8 in control state                | Direction bits: forward `01`, reverse `02`, left `04`, right `08`; combine throttle and steering bits |
| Byte 9                                 | Manual speed magnitude; zero when no throttle is requested                                            |
| Byte 10                                | Flags; forward uses bit 0, reverse leaves it clear in these tests                                     |
| Byte 11                                | `64` in the decoded ordinary manual state                                                             |
| Bytes 12–13                            | Zero in the implemented manual controls                                                               |
| Byte 14                                | Counter, 1 through 250, then back to 1                                                                |
| Bytes 15–17                            | Zero in the implemented manual controls                                                               |
| Byte 18 in transmitted requests/states | Start with `E9`, XOR each of bytes 0–17                                                               |

The app's received pairing-response check does not validate an equivalent reply checksum. Do not infer one from the transmit formula. Also distinguish the 19 application bytes from the enclosing manufacturer advertisement structure: AD length/type and company ID add bytes around it. Windows exposes those parts separately.

The standard-car light flag is now physically verified on the tested unit: payload byte 10 bit 2 (`0x04`) turns the lights on while throttle and steering are neutral. The app's light diagnostic and driving toggle use that tested flag. Horn behavior and GATT compatibility with this physical unit have not been established.

## Contributions that would move the project forward

| Question                                     | Useful evidence or extension                                                                                                        |
| -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Which retail variants support this protocol? | Exact listing and revision, photographs, app identifiers, and reproducible pairing results from additional units                    |
| What is the unmarked radio chip?             | Clear board photos, a positively identified matching board, or documentation tied to that revision                                  |
| How does the handheld remote work?           | Radio observations during button presses and while idle; do not infer its protocol from the casing label                            |
| Why did the early attempts fail?             | Controlled tests of power-on timing, prior binding, and reception; the current history does not isolate a cause                     |
| Can the controller support arbitrary cars?   | Explicit target selection from learned replies, configurable/persisted vehicle identity, and testing with more than one nearby unit |
| What happens when traffic disappears?        | Measured light behaviour and motor/steering response under controlled signal loss                                                   |
| How do speed settings behave?                | Physical tests of 40/70/100, forward versus reverse, combined steering, and release latency                                         |
| Can it run on another platform?              | An implementation capable of BLE advertising, checked against the known payloads and physical observations                          |
| What else can the app control?               | Separate verification of accessories and other vehicle-family builders rather than assuming the standard-Car layout applies         |

For a useful report, include the car/board details, OS and adapter, experiment used, full relevant log, power-on sequence, and what the wheels and lights actually did. Distinguish a software error, no reply, a matching reply, and a physical action. A negative result can be valuable when its conditions are clear.

## Evidence to keep with the project

Keep the distinction between source code, recorded observations, and third-party application artifacts visible when extending the repository. A new controller feature should document what changed and how it was tested; a new compatibility claim should include the actual car evidence. No retail compatibility list, firmware dump, or chip identification should be implied by the single successful unit described here.
