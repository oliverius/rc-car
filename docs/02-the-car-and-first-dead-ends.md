# 02 — The car and the first dead ends

[Start here](01-start-here.md) · [Next: the Android app](03-finding-and-reading-the-app.md)

## A toy with very little identification

The starting point was a small Land Rover Defender G4 Challenge-style RC toy. The owner had a handheld controller and wanted computer control, initially using Python. The car had no camera. Opening it did not yield a readable chip part number, so there was no identified radio datasheet to work from.

The surviving notes contain these clues:

| Item                   | Recorded observation                           |
| ---------------------- | ---------------------------------------------- |
| Underside of car       | `NO 6601`                                      |
| Main circuit board     | Owner later reported `H7MJD6601-001`           |
| Earlier board spelling | `H7MD6601-001`; retained in the original notes |
| Handheld remote        | Marked `TRASPED` and `2.4GHz CONTRO`           |
| Remote controls        | Forward, reverse, left, right, and speed       |
| Radio chip             | No readable identifying marking reported       |

These are identification clues, not a proven manufacturer or hardware-revision catalogue. In particular, a PCB label is not a chip identifier. The spelling difference in the board notes should remain visible until photographs or other evidence settle it.

## Why scanning did not immediately help

The early plan was familiar: find the car in a BLE scanner, connect, inspect its services, and work out which characteristic controlled the motors. The initial notes and Python experiment followed that idea.

nRF scanning did not identify the car at power-on. It also did not identify a transmission attributable to the handheld remote when buttons were pressed or while the remote was controlling the car. Trying the phone app's different options, including after enabling location, did not produce a successful current app-control session at that stage.

The failed scans were real observations. What they did **not** establish was that the car had no BLE radio. Later, the car replied to a particular advertised pairing request. A scan that finds no recognizable name is not equivalent to exercising that exchange.

## The explanation involving two versions

During the search, we encountered an explanation that similar-looking cars existed in two versions: a basic handheld-RF-only version and an app-capable version with BLE. That sounded like a possible explanation for a car that worked with its remote but did not appear in nRF.

The saved source for this distinction was a Google-generated answer supplied during the investigation. It did not provide an exact model-to-board mapping or a verified comparison of two physical units. Other parts of the same conversation suggested Classic Bluetooth, an external RF dongle, or audio signalling. These explanations were not consistent enough to treat as a hardware specification.

For a future buyer, the useful lesson is to check the **exact advertised variant and evidence of app support**, not just the body shape or the word “2.4 GHz.” The stronger claim—exactly which board belongs to an RF-only or BLE version—remains open in this project. We did not obtain and compare two versions ourselves.

The later result answers the question for this particular unit: **it accepts BLE control packets**. The earlier idea that this board lacked BLE cannot explain that observation. Whether another visually identical car is RF-only still needs evidence specific to that car.

## What we could not infer from the remote

Pressing left or right a few times was enough for the owner's handheld controller to establish control and stop the car's flashing indication. We did not capture the remote's radio traffic. It might continue transmitting after a button press, or it might use different link behaviour; neither possibility was established.

Likewise, the lack of readable chip markings did not justify naming a likely chip vendor, assuming a second Bluetooth chip was necessary, or identifying the remote as a BLE central. Some early notes made stronger assumptions than the later evidence supported. Those assumptions are not prerequisites for reproducing the working laptop experiment.

At this point, the most promising source of information was the app that was supposed to control this family of toys.
