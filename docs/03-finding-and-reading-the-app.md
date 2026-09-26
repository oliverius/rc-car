# 03 — Finding and reading the Android app

[Previous: the car](02-the-car-and-first-dead-ends.md) · [Start here](01-start-here.md) · [Next: a radio reply](04-getting-a-reply.md)

## An app archive became the main source of evidence

An Android app archive was obtained from APKPure. The saved archive name was `RCCar_202606291657_APKPure.xapk`; the base APK was retained locally as `com.DeliMiniChe.carapp.apk`. The exact download-page URL was not preserved in the original notes available for this guide. The package identifier and version below are more precise references than the general name “RC Car.”

| Field                  | Analysed artifact                                                  |
| ---------------------- | ------------------------------------------------------------------ |
| Android package        | `com.DeliMiniChe.carapp`                                           |
| Version name           | `202606291657`                                                     |
| Version code           | `37`                                                               |
| Base APK SHA-256       | `71705595770fb2c068750ba6f95986ba51f50924e959460f04244d18861f9ab5` |
| Native library         | ARM64 `libil2cpp.so`, 35,435,856 bytes                             |
| Native library SHA-256 | `130d91cb0be6fecc06ace4d57e516186357a4ed358ba234fc0c4e881002ede55` |

These identify the files analysed, not a promise that a future download contains the same build. The application was examined statically: the analysis tools read its files and instructions. We did not need a successful running app session to recover the control format.

## Why the first APK was not enough

The base APK contained Android Java code, Unity metadata, and assets, but no native libraries. The Java layer showed how the app could scan, connect, write characteristics, and advertise. It did not establish which of those operations the car controller actually selected.

This was a split application. The missing logic was in `libil2cpp.so`, supplied through the matching ARM64 split, `config.arm64_v8a.apk`. Unity's IL2CPP build had compiled the application logic into native code. Reading only Java methods or UUID strings would have left the central questions unanswered.

The investigation used Androguard for the Android code, Il2CppDumper to recover names and metadata relationships, and ARM64 disassembly with Capstone and pyelftools to inspect the native instructions. UnityPy helped examine the app's menus and assets.

## First lead: a conventional GATT path

One implementation scanned for names containing `RCCar` and used a service ending in `FFF0`. It wrote controls through `FFF2`, expected `FFF1`, and initialized a six-byte client identity through `FFF4`.

That made a scan/connect/write experiment reasonable. Decoding a valid path in an app did not show that our physical car exposed those services. Searching harder for an `RCCar` name was not enough to establish compatibility.

## The important discovery: control through advertisements

The app also implemented a standard-Car controller that paired and sent movement using BLE advertisements. Its connection flow tried this broadcast exchange first and fell back to GATT if it did not receive the expected reply.

The successful route was therefore:

1. Advertise a pairing request containing a three-byte client identity.
2. Listen for a reply that echoes that identity and supplies a three-byte vehicle identity.
3. Put both identities into subsequent control advertisements.
4. Continue broadcasting the current state, including neutral when the controls are released.

This is application-level pairing. It does not require Bluetooth bonding, a persistent connection, or a GATT write for the route that ultimately worked here.

The difference explained why the original approach could miss the car: our goal was not necessarily to discover an always-visible peripheral with a recognizable name and connect to it. We needed to speak the request/response protocol it understood.

## The app supported more than one kind of toy

Menu and controller analysis identified ten family labels, including Bigfoot, Climbing Car, Drift Car, Tank, and several other vehicles. These were app families, not a verified list of retail models. The standard-Car manual control path was associated with **Bigfoot**.

No reliable commercial-model mapping to our board marking was found. The successful physical tests later connected this particular car to the decoded standard-Car packets. They do not make the same packet layout appropriate for every other toy in the app.

## Enough information to build a bounded test

The analysis established a 19-byte application payload, manufacturer/company ID `0000`, pairing opcode `06`, control opcode `07`, and expected reply opcode `0A`. It also established direction bits, speed encoding, a changing counter, and an XOR checksum.

The company ID is separate from the application payload in the Windows API. Accidentally including it twice would produce the wrong bytes. Similarly, the broadcast path's three-byte client identity must not be confused with the GATT path's six-byte identity.

The next question was no longer “what bytes might work?” It was “can Windows transmit these bytes, and will this car answer?”

For the detailed tools-and-evidence walkthrough, continue to
[07 — Inside the APK](07-static-analysis-walkthrough.md). It explains how the
native calls, field copies, helper loops, coroutine branches, and asset links
established these conclusions. To continue the physical experiment story,
read [04 — Getting a reply](04-getting-a-reply.md).
