# 07 — Inside the APK: the static-analysis walkthrough

[Previous: continuing the work](06-trying-another-car.md) · [Start here](01-start-here.md) · [App overview](03-finding-and-reading-the-app.md) · [Next: application architecture](08-application-architecture.md)

The important static-analysis result was a traced path from a joystick input to the bytes passed to Android's BLE advertiser. This chapter explains how we got there, the tools used, and the details that changed the investigation's direction.

This is a walkthrough of the recorded analysis, not a new decompilation or a claim to have recovered the original source. No app or native-library code was executed during that analysis. The later Windows experiments supplied separate physical evidence. All addresses below refer to the particular ARM64 library identified by the hashes in [chapter 03](03-finding-and-reading-the-app.md); they should not be carried over to another version without checking.

## 1. Inventory before interpretation

The first step was to inspect the APK as an archive. Python's `zipfile` module exposed the contents without installing it:

| Artifact                   | What it gave us                                                                                  |
| -------------------------- | ------------------------------------------------------------------------------------------------ |
| `AndroidManifest.xml`      | Package/version information, permissions, activities, and split requirements                     |
| `classes.dex`              | 1,865,624 bytes of Android-side code, including the BLE bridges                                  |
| `global-metadata.dat`      | 6,275,448 bytes of Unity IL2CPP metadata: names, fields, methods, and string-literal information |
| `ScriptingAssemblies.json` | Names of Unity scripting assemblies, not their executable implementations                        |
| `data.unity3d`             | Unity assets useful for understanding menus and controllers                                      |
| Missing `lib/` directory   | The base APK alone did not contain the native application logic                                  |

The manifest declared BLE scanning, advertising, and connection permissions, and required the BLE hardware feature. It also declared ABI split requirements. Those facts supported looking for the matching native split; they did not say which vehicle used which transport.

The manifest included PairIP application/licensing entries. Their presence was recorded, but the protocol work did not depend on bypassing licensing or running the protected application.

## 2. The tools, and what each actually did

These are the versions recorded in the investigation, not recommendations to install whatever currently has the same name.

| Tool             | Recorded version                                                          | Role and useful output                                                                        |
| ---------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Python `zipfile` | Standard-library tool; Python version not pinned in the original findings | Inventory and extraction of APK/XAPK members                                                  |
| Androguard       | 4.1.4                                                                     | Decode the manifest and disassemble Bluetooth-related DEX classes                             |
| Il2CppDumper     | 6.7.46                                                                    | Associate IL2CPP metadata with native methods; generate `dump.cs`, `script.json`, `stringliteral.json`, and dummy assemblies |
| Capstone         | 5.0.9                                                                     | Decode ARM64 instructions at methods and call sites of interest                               |
| pyelftools       | 0.33                                                                      | Read ELF structure and support address/relocation interpretation                              |
| UnityPy          | 1.25.3                                                                    | Read Unity objects, scene/prefab relationships, textures, and sprites                         |

The local analysis scripts were small tools for inspecting evidence, not the eventual car controller. `analyze.py` handled the base APK/DEX and metadata search; `native_inspect.py` produced annotated native listings. Catalogue work used `catalogue_assets.py`, `catalogue_links.py`, `catalogue_hierarchy.py`, `catalogue_sprites.py`, and `catalogue_native.py`.

The distinction between outputs matters. Androguard's listings were Dalvik disassembly, not reconstructed original Java source. Il2CppDumper's declarations and dummy assemblies gave names and structure, not recovered executable C# method bodies. The native listings supplied the instructions needed to check behaviour.

## 3. Two Java bridges, but still a missing decision-maker

The DEX contained two relevant integrations:

| Bridge                                                      | Observed responsibilities                                                                    |
| ----------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `com.shatalmic.unityandroidbluetoothlelib.UnityBluetoothLE` | Scan, connect through GATT, discover services, and write caller-supplied characteristic data |
| `com.sthjd.mylibrary.HjdUnityBluetoothLE`                   | Scan raw advertisements and publish manufacturer data                                        |

The GATT bridge accepted UUIDs, addresses, payloads, and write-mode options from its caller. Finding a generic write method did not tell us which characteristic the standard-Car controller used, or which bytes meant forward.

The advertising bridge was more revealing. Its `androidStartAdvertising` method set connectable to false, passed company ID zero and the caller's byte array to `addManufacturerData`, then called Android's advertiser. Its receive path forwarded raw records only when the first two bytes were `16 FF`.

Those were strong clues to a custom advertisement protocol. They still left the application-level question open: which controller called that bridge, and under what conditions?

Metadata searches found `RCCar`, the FFF0/FFF1/FFF2/FFF4 UUIDs, and names such as `DeviceName` and `HjdBluetoothHardwareInterface`. Initially these were candidates. A string's presence did not establish its role. One particularly misleading candidate, `link-ble-RCCar`, was later located in camera UDP controller code; it was not the name predicate to use for the standard GATT scan.

## 4. Recovering the native map

The matching ARM64 split supplied `libil2cpp.so`. Paired with the base APK's metadata, Il2CppDumper recovered metadata version 31 and located CodeRegistration at `0x1F846B8` and MetadataRegistration at `0x201A940`.

There were two practical distractions: an initial possible-protection warning, and a failure in the tool's final console-key wait under redirected input. The latter happened **after** output generation. Rather than treating the exit as proof that everything failed, the analysis checked the generated files and validated their mappings against native instructions.

Native references in this project are ELF virtual addresses/RVAs, not raw byte offsets into the file. The inspection tooling used ELF structure and relative relocations to annotate references conservatively. Generated names helped navigate, but actual loads, stores, branches, and calls established the findings.

For example, a field named `cipherUUID` does not prove encryption. A helper named `cal_AndroidBle_sumdata` does not prove an additive checksum. Both needed their instructions inspected.

## 5. Turning the GATT clues into a real path

The static constructor `StartingExample..cctor` at `0xF948C0` assigned the `RCCar` name and FFF0/FFF1/FFF2/FFF4 UUIDs. That connected the literals to specific roles.

The discovery callback at `0xF94C6C` required a name containing `RCCar` and a manufacturer-data comparison. An interesting detail was hidden in the comparison helper: although the stored reference was a 15-byte UTF-8 string, `恒冠迷你车`, `data_isalike` at `0xFACBB4` compared only the first **seven** bytes, `E6 81 92 E5 86 A0 E8`.

This changed what a faithful scanner would check. Matching the whole string would be stricter than the app. Dropping two presumed company-ID bytes would also change the comparison: the Java callback passed the manufacturer AD data after length/type, including the bytes a structured scanner might expose separately as company ID. The reconnect callback had a different, name-based branch, so the seven-byte predicate was not universal to every connection attempt.

Service discovery then required FFF1, FFF2, and FFF4 under FFF0. The ordinary standard-Car GATT route initialized FFF4 with a six-byte client identity before enabling control writes to FFF2. That identity came from a persisted `CustomDeviceID` recipe involving device-identifier characters and Guid bytes; it was not a universal constant hidden in the APK.

The standard movement path used `StartingExample.SendByte` at `0xF94514`, requesting writes with response. A separate similarly named method, `sendData`, requested writes without response. Following the actual caller prevented choosing a write mode merely because both methods existed.

This was a decoded app implementation. It did not establish that our physical car offered those GATT services. The eventual successful experiment used the other route.

## 6. Following the joystick into the advertiser

For the standard-Car/Bigfoot broadcast path, the trace linked these stages:

| Stage                         | Method or location                                           | What it established                                                     |
| ----------------------------- | ------------------------------------------------------------ | ----------------------------------------------------------------------- |
| Read joystick/buttons         | `ControlCarOperUI.Update`, `0xF67650`                        | UI input reaches shared axis/button fields                              |
| Select ongoing processing     | `ContainerRemoveCarOperUIController.Update`, `0xEE6190`      | Calls broadcast processing while application connection state is active |
| Build current broadcast state | `ProcessBroadcastConnection`, `0xEE659C`                     | Invokes the builder, copies its fields, and includes the counter        |
| Encode manual controls        | `BuildControlData`, `0xEE89E0`                               | Direction, speed, flags, constant and auxiliary byte                    |
| Wrap identity and state       | `CreateCarbroadData`, `0xEE82B8`                             | Creates the 19-byte application payload                                 |
| Prepare advertising           | `HjdBluetoothHardwareInterface.StartAdvertising`, `0xE18F00` | Applies the checksum before the Java call                               |
| Compute checksum              | `cal_AndroidBle_sumdata`, `0xE1905C`                         | XOR seeded with `E9` over bytes 0–17; final byte is the result          |
| Hand off to Android           | Java `androidStartAdvertising`                               | Publishes the array as manufacturer data with company ID zero           |

This was stronger than noticing that broadcast bytes resembled the GATT format. `ProcessBroadcastConnection` actually called the builder at `0xEE6660`, copied its five output bytes into a newly allocated ten-byte state array, and placed `_miscodeIndex` at state byte 6. The wrapper copied that state into application payload bytes 8–17.

The zero-initialized allocation explained the unused zero bytes without assigning them unsupported firmware meanings. The wrapper supplied family `04`, opcode `07`, the learned vehicle identity, and the client identity. The checksum helper modified byte 18 of that same buffer. Its instructions used XOR, not addition, and the company ID was outside the loop. No additional encryption or transformation appeared in this traced sender.

### Speed and direction were separate discoveries

Direction stores established forward `01`, reverse `02`, left `04`, and right `08`, combined as bits. Forward also set flag bit 0; reverse in ordinary manual mode left that flag clear.

In the broadcast manual branch, the instructions multiplied throttle magnitude by the selected maximum rate, truncated the result to an integer, and stored a byte. The relevant ARM64 sequence included `FMUL`, `FCVTZS`, and `STRB` at `0xEE90C8`–`0xEE90D8`. This is why the first experiment could request decimal speed 20 directly.

Other branches had GATT-specific boosts and a separate cruise encoding. Importing those values into manual broadcast control would have mixed protocols. Steering here was represented by direction bits; a separate proportional steering magnitude was not demonstrated.

The builder sets bit 2 (`0x04`) in payload byte 10 when the app's `active_led_state` is set. Byte 11 remains the constant `0x64`. Initially this mapping was only evidence of what the Android app requested, not proof of a physical effect. A later stationary test held throttle and direction at zero, kept byte 11 at `0x64`, and compared byte 10 values `0x00` and `0x04`. When testing this, the owner reported that the lights turned on during the `0x04` state. This confirms the flag's light-on effect for the tested car and neutral state; it does not establish behavior for other models or whether the lights remain on after the flag is cleared.

The other accessory mappings, including horn and additional LED controls in other vehicle-family controllers, still need independent physical tests. Do not assume those fields or behaviors carry over to the standard-car broadcast protocol.

## 7. Pairing, identities, and the reply predicate

`CreateDuimaData` at `0xEE7C30` built the pairing payload: family `04`, opcode `06`, client bytes in positions 5–7, and zeros elsewhere before checksum insertion.

The app's broadcast client identity came from the low bytes of the first three characters of Unity's device-identifier string. They were character values, not a hexadecimal decoding. The experiment's `61 62 63` corresponds to the characters “abc”; it was a chosen consistent identity, not a recovered secret. It must not be confused with the six-byte GATT identity.

After Java's leading-`16 FF` filter, `OnScanDataReceived` at `0xEE7F18` decoded the Base64 record and called `calDataIsTrue` at `0xE19610`. That helper checked the raw length byte, family, response opcode `0A`, and echoed client bytes. Despite its name, it did not verify a reply checksum, company ID, name, MAC address, or service UUID.

The callback then copied raw record bytes 6–8 into `tankeaddr`, the vehicle identity later placed into control payload bytes 2–4. It set the broadcast/application connection flags and prepared control advertising. A scheduled half-second transition closed the pairing UI; it was not an additional GATT handshake.

This analysis also explained a parsing difference in the Windows diagnostic: Windows exposes parsed manufacturer sections, so its matcher can find the expected payload even if that section is not first in the complete advertisement. The original Android bridge's leading-prefix filter was more restrictive.

## 8. Reconstructing timing from a coroutine

The connection button led to `StartBle`, then the `AdvConnect` coroutine. Its compiler-generated `MoveNext` method at `0xEEA4F8` was a state machine, not straight-line source code. Resolving its jump table was necessary to put the blocks in execution order.

The decoded sequence was: stop existing advertising, wait **0.1 seconds**, start the pairing advertisement and scanner, wait **1.5 seconds**, stop both, wait **0.5 seconds**, and fall back to GATT if broadcast pairing had not succeeded. The first delay came from the loaded constant and instructions, not a suggestive metadata name. Cleanup ran even after a reply and could briefly interrupt advertising until a control update restarted it.

Ongoing movement had two other clocks:

| Mechanism             | Decoded app behaviour                                                                                 |
| --------------------- | ----------------------------------------------------------------------------------------------------- |
| Input-change throttle | Three roughly 50 ms timer ticks, about 150 ms, before another changed state could replace advertising |
| Steady-state refresh  | Twelve ticks, about 600 ms, advanced the counter even when joystick state was unchanged               |
| Radio repetition      | Android continued advertising the active payload between application updates                          |

The counter began at 1 and wrapped from 250 to 1. It participated in the state comparison, which is why holding still or leaving the joystick neutral still led to new states. These were frame-dependent application timers, not precise measured RF intervals. The GATT branch's separate 20 ms and 50 ms coroutines were not the broadcast loop and were not used to infer its cadence.

Pointer release zeroed the joystick input and generated neutral. It did not stop the advertiser. That static finding later made sense of the owner's flashing lights after the finite test scripts exited.

## 9. Assets supplied the menu-to-controller map

The native methods explained packet construction, but we also wanted to know which visible app selection reached each implementation. UnityPy inspected the base `data.unity3d` and the asset pack's `datapack.unity3d`. Their serialized data reported Unity `2022.3.62f3c1`.

Generic type-tree parsing could not fully decode many stripped MonoBehaviours. Instead, the catalogue work followed common-header references to MonoScript and GameObject objects, reconstructed Transform parent relationships, and recorded strings with offsets. Those were evidence-bearing object links, not an invented reconstruction of every C# field. Unity object path IDs were local serialized identifiers, not vehicle product IDs.

The route was established through button callbacks, prefab or scene names, attached scripts, and the corresponding native methods. For example, the Bigfoot callback at `0x109AF8C` loaded `ContainerRemoveCarOper`, leading to the standard-Car controller.

Some of the useful surprises were:

* Ten entries were selectable across four menus: Bigfoot, Climbing Car, Drift Car, Chache, Digger, Forklift, Robot, Track, Boxing, and Tank. This was an app-family list, not a retail compatibility list.
* The image for **Chache** depicted a forklift, while the internal **Forklift** entry depicted a dump truck. Names alone would have mislabelled them.
* Standard Car, Climb, and Drift GATT state buffers were respectively 10, 8, and 12 bytes. Several other families shared UUIDs or a ten-byte length but used independent builders.
* Chache and Forklift both used an internal type value `09`; Digger, Boxing, and Tank shared `0C`. These collisions ruled out treating the values as globally unique product identifiers.
* Tank used a distinct status path with an FFF1 subscription and received `hpValue`, despite sharing the main UUIDs. Full combat semantics were not decoded.
* Selected Robot and Track camera scenes attached UDP controllers that called `UdpClient.SendAsync`. The presence of BLE classes elsewhere did not make those selected routes BLE.
* Drift had advertising pairing/settings code, but that alone did not establish a full broadcast movement route for it.

Sprite extraction and contact sheets helped interpret the menus. Manuals, camera scenes, and programming/gravity/time/route modes also appeared in the assets. Their presence did not imply that every mode or old prefab was reachable for every physical toy.

A bounded search did not find a textual commercial mapping for Defender, Land Rover, TRASPED, or the board markings. That was a useful negative result, not proof that no image contained a brand name. It prevented the catalogue from being presented as an authoritative list of compatible products.

Static analysis established what this Android app requested. It could not identify the unmarked chip, prove the handheld remote's protocol, determine every firmware timeout, or show that every retail variant used the same implementation. The later pairing and wheel observations supplied the missing evidence for one physical car. Keeping those two evidence sources separate is what makes this investigation reproducible rather than merely a plausible story.
