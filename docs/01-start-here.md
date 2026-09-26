# 01 — Controlling a tiny RC car from a Windows PC

This project began with a small toy car, a working handheld remote, and a question: could we control it ourselves from a computer? The car did not show up as an obvious Bluetooth device, and the chip inside had no readable identifying markings. Finding the right protocol took more than discovering a device name and connecting to it.

By the end of this experiment, I could drive and steer the car using the arrow keys in a Windows controller. The working route uses **Bluetooth Low Energy advertisements**: short broadcasts containing pairing requests and control states. It does not require a normal Bluetooth pairing entry or a GATT connection.

This guide tells the investigation in order, including the false starts. It is written for someone who buys a similar car online and wants to understand what is known, what to try, and where they could contribute. It describes one tested car, not a universal driver for every car with a similar body or remote.

## Read in order

| Chapter                                                                               | What it covers                                                                                             |
| ------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [02 — The car and the first dead ends](02-the-car-and-first-dead-ends.md)             | Unmarked chip, nRF scanning, remote control, and the two-version explanation                               |
| [03 — Finding and reading the Android app](03-finding-and-reading-the-app.md)         | APKPure, split APKs, Unity native code, and the two BLE paths                                              |
| [04 — Getting a reply over the air](04-getting-a-reply.md)                            | Failed Windows attempts, independent phone reception, and the first matching reply                         |
| [05 — From a reply to a working controller](05-from-packets-to-driving.md)            | Forward, steering, reverse, flashing lights, arrow keys, and gears                                         |
| [06 — Trying another car and continuing the work](06-trying-another-car.md)           | Compatibility limits, project map, protocol reference, and useful contributions                            |
| [07 — Inside the APK: static-analysis walkthrough](07-static-analysis-walkthrough.md) | Tool versions, DEX/native evidence, joystick-to-radio trace, coroutine timing, and Unity asset discoveries |
| [08 — Application architecture](08-application-architecture.md)                       | The subsequent three-project WPF toolkit, interfaces, radio lifecycle, and tests                           |

Chapter 07 is the technical companion to chapter 03. Read it there if you want
the detailed reverse-engineering trail, or after the main story if you plan to
continue the analysis.

## What the result actually establishes

The tested car accepted our BLE pairing request and control packets. I observed drive-wheel movement, backward motion, steering in both directions, and return to centre. I subsequently reported that the interactive controller worked. The original controller used speed byte 20; selectable 40%, 70%, and 100% gears were added afterward and passed offline checks, but no separate physical result for those gear settings had been recorded when this guide was written.

A packet log records what software requested and what Windows reported. The owner's observations establish what the car physically did. Both matter. In particular, a successful publisher status alone is not proof of radio reception or motor movement.

[Continue to 02 — The car and the first dead ends](02-the-car-and-first-dead-ends.md)
