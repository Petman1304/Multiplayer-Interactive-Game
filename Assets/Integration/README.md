# Blind Getaway integration

Open `Assets/Scenes/IntegratedGetaway.unity` (first in Build Settings). It now uses
one daylight Downtown district with a baked, approximately 1,800 m route, four
45-degree bends, six city module variants, and repeating endless survival.
See [environment authoring and acceptance results](Environment/README.md).

## One-device Solo Debug

Press Play manually, then choose **Solo Debug** on the main menu. One local host
simulates both roles; no build or second client is required. It starts in the
driver seat. **Tab** switches both POV and active controls.

| Role/action | Keyboard and mouse |
| --- | --- |
| Driver | A/D or arrows: steer through bends and dodge; mouse: look from seat |
| Shooter | A/D or arrows: balance in the cargo bed; mouse: aim |
| Shoot/reload | Hold left click or Space; R reloads; right mouse zooms |
| Settings | Esc releases controls and mouse; simulation continues |
| Performance capture | F8 starts/stops a manual ten-minute CSV capture in Editor/development builds |

Inactive-role controls release; there is no partner bot. Leaving the driver during
a bend can cause a crash, and leaving the shooter can cause a fall. Solo Debug
rejects remote joins and bypasses readiness only for its own session. It does not
verify actual network latency or synchronization.

## Two-player LAN test

1. Manually run two instances, preferably one on each laptop after freeing memory.
2. Choose **Host Crew** on one. Choose **Join Crew** on the other, enter the host's
   LAN IPv4, and connect. Same-machine builds can use `127.0.0.1`.
3. Select different Driver/Shooter roles, select a local controller in Settings,
   both Ready, and let the host choose Start Survival. Selecting your current
   role again releases it for swapping.
4. Driver sees the road normally. Only the shooter receives bend direction,
   distance/time to entry, ammunition, and balance information. Communicate verbally;
   built-in voice chat is not implemented.
5. After defeat, the host returns the crew to the lobby. A partner disconnect
   returns the remaining host to the lobby; losing the host disconnects the client.

Direct-IP LAN uses UDP 7777, with no Relay, matchmaking, or automatic discovery.
Both instances must use the same route assets/build and v2 integration protocol.
Older v1 clients are rejected because route-coordinate serialization changed.

## Primary custom controllers

Choose your role before connecting hardware. Select **Custom Controller**, enter
`COM3` or `/dev/ttyACM0`, centre the device, and choose Connect Device. Close the
Arduino Serial Monitor first. Port access uses normal OS permissions.

- Driver: **9600 baud**, one invariant-culture float plus newline. The full-range
  slider maps the change from neutral to full steering; default 1.57. Encoder
  radians and older raw values can be calibrated. Invert Steering changes polarity.
- Shooter: **115200 baud**, `roll,pitch,yaw,aim,shoot` plus newline. Angles are
  radians and buttons are 0 or 1. Tilt balances, yaw/pitch aim, aim zooms, shoot fires.
- **C** or Calibrate resets neutral. Reconnect after changing roles. Device data
  older than 0.5 s releases local controls; host input expires after 0.35 s.
- Reading runs on one background thread. Missing devices report status without
  blocking rendering. Invalid/non-finite packets are ignored.

Hardware remains the intended main controller. Keyboard/mouse testing works
without a custom device; mixed hardware/keyboard multiplayer is supported.

## Gameplay and presentation

The host owns roles, ready checks, countdown, scripted movement, curve drift,
encounters, balance, oriented swept collisions, health, capture pressure, ammunition,
shots, and score. Clients submit bounded input at 25 Hz; snapshots update at 20 Hz.
There is no Rigidbody vehicle simulation. Speed rises to a maximum 42 m/s.

Truck hull starts at five. Barricades take one hit, traffic two, police three.
Crashes grant 1.5 s damage protection. Nearby police increase pursuit and attack
at three-second intervals; destroying police reduces pressure. Repair packs
restore one hull and add 24 reserve rounds (maximum 120). Magazine size is 12;
reload takes 1.8 s and empty magazines reload automatically when reserve remains.
Defeat reasons are TruckDestroyed, Captured, and FellOff. There is no finite win
condition, branching route selection, pedestrian simulation, or replay capture.

The full `Truck ver 1` body is retained. Driver view adds `kemudi` without duplicate
cab surfaces, uses the DriverEyeMount, and has no driver avatar. Only the curved
windshield gets a transparent runtime material. Right steering rotates the rim
clockwise from the driver's seat. The shooter stands just behind the cab at
`(0, 1.732, 0.65)` with a 2.2 m eye offset and sees no `kemudi` assembly.

Original player artwork is copied as rendering geometry, excluding legacy input,
serial, camera, and physics scripts. Downtown modules use shared original Petrus
meshes/materials and simple authored furniture, with no decorative colliders.

## UI and quality

Menus use the original `UI Button` background at full screen. The reusable
`Prefabs/GetawayButton.prefab` supplies purple gradient, cyan outline, font,
button states, and editable labels for existing and new actions. Settings and
HUD share that palette. Original UI source scenes are unchanged.

Settings expose volume, steering sensitivity/range/inversion, keyboard/custom
mode, serial connection, calibration, and graphics. Downtown graphics cycle
between **Low** and **Balanced**, defaulting to Low. Their pipeline assets and
runtime daylight overrides are isolated from the original project presets and
restored when the integration scene unloads.

## Verification tools and limits

All tools are opt-in. No build, Play session, second Editor, or sustained profiler
run is started automatically. The new district has passed pure geometry/network
serialization checks, bounded recycling checks, and small first-person previews.
Live multiplayer, physical-controller validation, sustained memory stability, and
actual 60 FPS acceptance still require the manual tests in the environment guide.

`IntegrationBuild.Verify()` checks packets, legacy geometry, downtown geometry,
and scene/prefab references. `VerifyAndBuild()` additionally creates a Linux
development build at `/tmp/getaway-integration-build/Getaway.x86_64`; run only
when intentionally building and sufficient memory is available.

Development builds retain explicitly requested `--getaway-smoke=host`,
`--getaway-smoke=client`, and `--getaway-smoke=third` automation. The runner now
uses route-aware aiming and steering but has not been executed for this milestone.

## Truck window visibility in builds

Both `Truck ver 1/Plano` (windshield) and `Plano.001` (side/rear glazing)
reference the integration's `TruckClearGlass.mat` in `TruckPrefab.prefab`.
The prefab reference includes the transparent shader variant in player builds;
runtime material clones preserve the same transparent configuration on both meshes.
Opaque doors, roof, pillars and dashboard retain their body materials. Rebuild both
clients after updating; an already-built executable does not pick up asset changes.
Front/side visibility was checked in an edit-mode preview. Linux player verification
remains manual; no build or Play session was launched for this correction.
