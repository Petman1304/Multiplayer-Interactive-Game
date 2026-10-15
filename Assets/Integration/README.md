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
Both instances must use the same route assets/build and v3 integration protocol.
Older v1/v2 clients are rejected because threat phases and full aiming require matching serialization.

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

Truck hull starts at five. Barricades take one hit, traffic two, rear police six.
Crashes grant 1.5 s damage protection. Rear police approach with a siren, hold at least 22 m behind, and raise capture
pressure by 4/s within 45 m after their warning. Pressure falls by 3/s outside
that condition, including bend quiet zones. Police never ram or periodically damage
hull. Defeating police removes 20 pressure; outrunning them by 75 m for 5 s ends
the chase. Pursuits last at most 30 s with a 12 s cooldown afterward. Repair packs
restore one hull and add 24 reserve rounds (maximum 120). Magazine size is 12;
reload takes 1.8 s and empty magazines reload automatically when reserve remains.
Defeat reasons are TruckDestroyed, Captured, and FellOff. There is no finite win
condition, branching route selection, pedestrian simulation, or replay capture.

The full `Truck ver 1` body is retained. Driver view adds `kemudi` without duplicate
cab surfaces, uses the DriverEyeMount, and has no driver avatar. The windshield and side/rear glazing use build-included transparent material variants. Right steering rotates the rim
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

## Moving traffic and police pursuit (v3)

The route remains endless. Anchors at 200/430/690/900/980/1050/1190/1700 m target
slow traffic, a blocked lane, a signalled adjacent lane change, recovery, rear police,
light traffic, staggered stationary barriers, and recovery. They are scheduling
targets: the host defers or skips unsafe waves. The first circuit permits four
active threats; the next permits six, then eight, within the existing 18 slots.
Later slow-traffic waves may contain two spaced cars; pursuit permits only one
straight-driving traffic car ahead and suppresses lane-changing/staggered waves.

Traffic speed is fixed on activation at 65% of truck speed, clamped to 14–28 m/s.
Amber indicators signal for 2 s before a 3 s smooth adjacent-lane change. The host
reserves its corridor; an unavailable or unsafe change falls back to staying in
lane if that is safe, otherwise the entire wave waits or expires. No new lane
change or pressure phase begins inside the 55 m bend-boundary quiet zones.

Shooter mouse aim now turns through 360 degrees. Hardware yaw uses calibrated
angular differences across the +/-180-degree boundary; packet format is unchanged.
A/D balance remains truck-relative. The shooter sees rear distance and pursuit
messages; both players hear the approach siren. Driver navigation remains verbal.

`LevelVerification.Run()` is an explicitly invoked edit-mode check; it never starts
Play or builds. Runtime/Editor source compilation and pure checks passed: 480
seed/circuit/speed candidates (404 reachable, 76 rejected), actual first-circuit
lane-change admission, occupied budgets, cooldown/single-pursuer policy, quiet
zones, expiration, four-second warning bounds, an impassable wall, rear shots,
pursuit timeout/escape, yaw seams, v3 serialization, and allocation-free traffic
movement. Route seams, oriented collision/rays, keyboard curve control and zero
route-sampling allocations also pass. Candidate checks cover three truck speed
profiles and a 36 s horizon using conservative swept boxes and a sampled reachable
corridor. They do not prove safety for every player action or future traffic state.

Manual acceptance remains outstanding: rebuild matching clients; test each role
hosted in turn, rear aiming/shooting, amber indicators and siren, defeat/rematch,
disconnect, Solo Debug Tab switching, and physical controllers. Compare one Low
build at 1280x720 with the Editor closed against the prior version, then measure
CPU/GPU frame times and a 10-minute memory capture separately. The pure-check suite
is not a frame-time benchmark or evidence of 60 FPS. No Play/build/profile run was
started automatically during this implementation.
