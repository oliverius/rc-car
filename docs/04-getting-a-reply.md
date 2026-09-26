# 04 — Getting a reply over the air

[Previous: the app](03-finding-and-reading-the-app.md) · [Start here](01-start-here.md) · [Next: driving](05-from-packets-to-driving.md)

## Windows could publish, but the first car tests were negative

A separate C#/.NET experiment used Windows' native BLE advertisement publisher and watcher. It transmitted only the decoded pairing request. Movement was deliberately deferred until there was a relevant reply.

A local capability check showed that the adapter could enter the publisher's Started state while receiving nearby advertisements. That established the local API capability. It did not yet establish a car response.

Two physical attempts that day also completed without a matching reply:

| Attempt                       | Received advertisement events | Matching replies |
| ----------------------------- | ----------------------------- | ---------------- |
| Five-second pairing window    | 33                            | 0                |
| Fifteen-second pairing window | 57                            | 0                |

The owner also reported no light change during the longer attempt.

These results were useful because they separated nearby radio reception from a matching car response. A successful process exit, or `Publisher Started`, could not be treated as pairing success.

## Use the phone to inspect the laptop, not just the car

The next useful check was independent reception. A phone scanner was used to inspect what the laptop was advertising while the car was off. The supplied screenshot showed legacy advertising, company ID `0000`, and this manufacturer application data:

```text
04 06 00 00 00 61 62 63 00 00 00 00 00 00 00 00 00 00 8B
```

That was the intended 19-byte request, including the correct padding and final checksum. The scanner reported an advertising interval around 103 ms. It also displayed the company label “Ericsson AB” for ID `0000`; that label did not identify the laptop or car manufacturer. It was the scanner's interpretation of the company-ID field.

This check ruled against the suspected extra-zero/payload-corruption explanation. It did not prove that the car received the request, nor did the cropped screenshot capture every over-the-air field.

## The breakthrough

The owner ran the Windows pairing experiment and switched the car on during the advertising window. This time the summary reported **17 matching packets**, all yielding vehicle identity **`F5 71 CD`**.

Selected lines from the owner-supplied excerpt:

```text
2026-09-25T15:29:48.0817262+01:00 RX CANDIDATE address=6666A1CD71F5 RSSI=-74 dBm type=ConnectableUndirected CompanyId=0x0000 length=19 payload=04 0A F5 71 CD 61 62 63 00 00 00 00 00 00 00 00 00 00 CE fieldsMatch=True expectedLength=True MATCH=True
2026-09-25T15:29:51.0508935+01:00 SUMMARY publisherEnteredStarted=True; completedWindow=True; receivedEvents=126; manufacturerSections=110; matchingPackets=17; suppressedUnrelated=87
2026-09-25T15:29:51.0510346+01:00 Vehicle identities from matching packets: F5 71 CD
```

| Reply field | Interpretation                        |
| ----------- | ------------------------------------- |
| `04`        | Standard-Car family/type              |
| `0A`        | Expected pairing response opcode      |
| `F5 71 CD`  | Vehicle identity returned by this car |
| `61 62 63`  | Echo of our chosen client identity    |

This was not the laptop recognizing its own outgoing request: the request used opcode `06`, while the received packets used `0A`. The power-on correlation strongly associated the reply with the car. Subsequent controlled motor tests supplied stronger physical evidence.

The record does not isolate what changed between the unsuccessful previous attempts and this success. We should not retrospectively credit a particular permission, battery state, binding reset, or timing change without evidence.

## What “pairing” means here

For this experiment, pairing learns the identity needed to address control packets. The fixed client bytes `61 62 63` were reused in the request and movement states. No operating-system Bluetooth pairing dialog was involved.

The Windows diagnostic checked the expected response fields and length. It was not a cryptographic authentication step. Its conservative message—“not independent proof of its physical source”—was a description of that limitation, not an error.

The observed Bluetooth address was `66:66:A1:CD:71:F5`. The application-level vehicle identity was `F5 71 CD`. Future owners should learn their own car's returned identity rather than assume either value is universal or derive it from an address without checking the reply.

The earlier RF-only explanation was no longer a good account of this unit. We now had a BLE reply. The next step was to ask for one small action and watch the wheels.
