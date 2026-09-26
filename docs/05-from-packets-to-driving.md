# 05 — From a reply to a working controller

[Previous: the first reply](04-getting-a-reply.md) · [Start here](01-start-here.md) · [Next: trying another car](06-trying-another-car.md)

## One action at a time

Once the vehicle identity was known, the experiments moved from reception to physical control. The wheels were to be kept clear for the initial tests, with the power switch accessible. Each movement experiment required a fresh matching pairing response before sending control states.

The initial speed byte was deliberately limited to decimal 20. A brief pulse and repeated neutral advertisements made the result easier to observe than an unrestricted controller would have been.

| Test     | Requested sequence                                  | Owner's observation                                                    |
| -------- | --------------------------------------------------- | ---------------------------------------------------------------------- |
| Forward  | Neutral, 0.3-second forward pulse, repeated neutral | “the wheels moved and stopped”                                         |
| Steering | Left, neutral, right, neutral; zero throttle        | Front wheels moved to one side, then the other, and returned to centre |
| Reverse  | Neutral, 0.3-second reverse pulse, repeated neutral | “it moved only once just a little, backwards”                          |

The forward and reverse publisher windows measured about 315 and 314 ms respectively. These are Windows event timings, not measurements of how long the motors actually ran. The small backward movement matched the intended one-shot test; it did not imply reverse was malfunctioning.

## A control packet and its neutral successor

The reverse log contains this command, shown here without wrapping changes to its payload:

```text
2026-09-25T15:52:40.9810447+01:00 TX REVERSE: 04 07 F5 71 CD 61 62 63 02 14 00 64 00 00 02 00 00 00 B3; requested hold=300 ms after Started
```

The significant fields were the control opcode `07`, our vehicle and client identities, direction `02` for reverse, speed `14` in hexadecimal (20 decimal), and the updated counter and checksum.

The next neutral state cleared direction and throttle:

```text
04 07 F5 71 CD 61 62 63 00 00 00 64 00 00 03 00 00 00 A4
```

Neutral is an actual transmitted state. Ending the advertiser is a different operation. Although the wheels stopped during the test, that alone did not isolate neutral reception from a possible firmware timeout as the stopping mechanism. We therefore continued to send neutral explicitly rather than relying on loss of signal.

## Why the lights started flashing again

After the steering test, the owner noticed that the car's lights were flashing as though it was no longer paired. The timing clarified the result: the lights were not flashing while the wheels moved. They began flashing approximately **one or two seconds after the script had completely stopped**.

That fits the decoded app's behaviour. While active, the app keeps advertising the current state, including neutral, and periodically changes a counter. Our one-shot experiment ended all transmissions after its final neutral period.

The observations strongly support an indication of lost ongoing control traffic. They do not establish the exact firmware timeout or every meaning of the lights. They also do not prove that the handheld remote behaves differently: we have not captured its transmissions while its buttons are released.

The practical consequence was clear enough for the controller: stay silent only when exiting, and continue sending neutral while idle.

## Arrow keys and a small Windows window

The next step was a interactive controller. A small window provided held-key and release handling, and a clear place to display pairing readiness. The owner had expected a command-line program but was happy with the result and reported that it all worked.

Arrow keys request forward, reverse, and steering; steering can be combined with throttle. Releasing an arrow neutralizes that axis. Opposite arrows cancel on their axis. When the window loses focus, movement is cleared, and the arrows must be released before driving resumes. Idle neutral refreshes continue even while the window is unfocused.

The controller refreshes steady states roughly every 600 ms, with an advancing counter, and replaces the requested state when input changes. Windows still controls radio scheduling. The displayed state is what the controller requests, not telemetry from the car.

The owner found the car a little slow. That was consistent with the initial fixed speed of 20; speed had not yet been exposed as a control.

## Space became a gear button

The first version used Space as an additional neutral/stop input. The owner pointed out that releasing forward already stopped the car and preferred Space to resemble the handheld controller's speed button.

Using the owner's stated three speeds, Space now cycles:

| Gear | Requested speed byte   |
| ---- | ---------------------- |
| 1    | 40, displayed as 40%   |
| 2    | 70, displayed as 70%   |
| 3    | 100, displayed as 100% |

The controller starts in gear 1. Each press advances one gear, wrapping from 3 to 1; holding Space does not repeatedly shift. The selected gear applies to both forward and reverse, including while an arrow is already held. Shifting while idle leaves throttle at zero. Escape still requests neutral cleanup and exits.

These are direct manual-mode speed values. They are not mechanical gears, a calibrated percentage of road speed, or the app's separate cruise-mode encoding. The change passed build and offline packet/input checks. A separate owner report validating the higher-speed settings had not yet been recorded.

## Where the experiment ended

The original goal was met: the owner could control the toy car from the computer. This established enough of the standard-Car BLE advertisement protocol to make the project useful, without pretending that every aspect of the car had been reverse engineered.

The chip remains unidentified, the remote's link remains unexplained, and compatibility with another retail unit is still a question to test. Those are good starting points for the next contributor.
