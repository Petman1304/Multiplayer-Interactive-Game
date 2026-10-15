# Blind Getaway integration

Open `Assets/Scenes/IntegratedGetaway.unity` and press Play. This scene is first
in Build Settings. It is a separate integration of the driver / roof shooter
endless survival loop, branch artwork, LAN networking and controller protocols.

## One-device solo debug

Press Play in `IntegratedGetaway.unity`, then select **Solo debug** in the bottom
action bar. Only one game instance runs; no build or second client is needed.
The local host simulates both jobs, while input controls only the role currently
being viewed. It starts in the driver seat. Tab switches to the roof shooter.

- **Driver view:** A/D or arrows steer; mouse looks around from the seat.
- **Shooter view:** A/D or arrows move/balance; mouse looks and aims; left click
  or Space fires; R reloads; right mouse zooms.
- **Tab:** switch active first-person role. **Esc:** release mouse/open settings.
- Inactive role controls release, but the simulation continues: the truck coasts
  and the shooter remains subject to inertia. No partner bot is added.

Solo debug rejects remote joins and bypasses the two-player ready requirement
only for its own session. It can test driving, shooting, damage, repairs,
wrecks and rematches, but cannot validate network latency or synchronization.
Leave the debug session before hosting a normal two-player session.

## Existing UI and artwork

The integrated scene now contains a copy of the original `UI Button.unity`
canvases, sprites, inputs, role buttons and EventSystem. `IntegratedSceneUI`
wires its Host, Client, Connect, Driver and Shooter buttons into the integrated
network session. A bottom action bar adds Ready, Start, controller settings,
Back/Leave, and Solo Debug. Clicking your selected role releases it for swapping.
The older level panel is hidden because this mode uses endless survival.
The original `UI Button.unity` file remains editable as the source design;
subsequent changes to it need to be copied into the integration scene.

The world references the existing truck, shooter and traffic prefabs and a
building mesh from the merged petrus scene assets. It preserves their rendering
hierarchy and animation controller while excluding their old local-only scripts.

## Two-player test

1. Use a standalone build plus the Unity Editor, or two standalone builds.
2. On one instance click **Host crew**. On the other click **Join crew** with
   `127.0.0.1` for the same PC, or the host's LAN IPv4 for a second computer.
3. Choose **Drive** and **Shoot** on separate instances. Select your local
   controller mode, both **Ready up**, then the host selects **Start survival**.
4. Survive traffic and police, destroy threats, and collect green repair packs.
   The truck has five hull points; crashes grant 1.5 seconds of damage protection.
   Traffic needs two hits, police three, orange barriers one. Police begin after
   20 seconds and slowly move toward the truck's lane. Speed and spawn frequency
   increase with survival time. The road and traffic presentation are pooled.
5. After a wreck, the host returns the crew to the lobby. To swap roles, click your
   selected role to release it before choosing the other role. A disconnect returns the remaining
   host to the lobby; host disconnection lets the client host or join again.

LAN/direct IP uses UDP 7777. Allow that port on the host firewall. This scene
has no Relay or internet matchmaking; two machines on different networks need
network routing configured separately.

## Keyboard and mouse testing

| Role | Controls |
| --- | --- |
| Driver | A/D or left/right arrows to steer; automatic acceleration |
| Shooter | A/D or left/right arrows to move across the roof |
| Aim | Mouse look aims the first-person view; right mouse zooms |
| Fire | Hold left mouse or Space |
| Menu | Escape opens local settings; the shared simulation continues |

Controls release when the window loses focus or local settings are open.
Keyboard/mouse is selected initially so a controller-free test works immediately.
Each instance selects its own device, allowing mixed keyboard/hardware sessions.

## Primary custom controllers

Select **Custom · primary** after choosing your crew role. Enter `COM3` (Windows)
or a port such as `/dev/ttyACM0` (Linux), hold the controller neutral, and click
**Connect**. Close Arduino Serial Monitor first. Port access follows the OS's
normal permissions. These are the existing repository firmware protocols:

- Driver: **9600 baud**, one invariant-culture float followed by a newline. Both
  the rotary encoder firmware's radians and the older steering firmware's raw
  values can be calibrated. The full-scale slider means the input change from
  center to full steering, initially 1.57. Use **Invert steering** when needed.
- Shooter: **115200 baud**, `roll,pitch,yaw,aim,shoot` followed by a newline.
  Angles are radians; the button values are 0 or 1. Tilt moves the shooter,
  yaw/pitch aim, aim button zooms, and shoot button fires. The code converts
  radians to degrees, matching the current Arduino sketch.
- **C** or the calibration button sets the current values as neutral. Reconnect
  after changing roles. Data older than 0.5 seconds releases hardware controls;
  packets older than 0.35 seconds release server-side movement and fire.
- Serial reading runs on a background thread. Missing or unplugged hardware
  shows status without blocking rendering. Malformed and non-finite packets
  are ignored. Hardware calibration and polarity still need a physical-device
  playtest.

## Network model

Unity Netcode and Unity Transport carry two crew-member network objects. The
host owns world simulation, role exclusivity, readiness, countdown, steering,
roof limits, traffic AI, collision, health, repairs, score, shot cooldown and hit
checks. Clients send bounded input at 25 Hz; shared state is sent at 20 Hz. Shot
feedback is delivered to both players. Input expires on the host if packets stop.
Host/client may take either role. Full and in-progress sessions reject joining.
Scene management is disabled because lobby and gameplay share one scene; the
integration uses its own prefab list and protocol version.

Vehicles remain close to the origin; distance is stored as a double and road
sections recycle. Artwork is copied as render-only hierarchy, preserving mesh,
materials, skeleton and animation controller while excluding old behaviour,
serial, collision and camera components. The scene uses the branch's truck,
shooter, traffic and building art. It uses a new shared simulation rather than
running the older local-only scripts on each machine.

## Verification

The optional `IntegrationBuild.Verify` helper checks parser, swept collision,
shot geometry, deadzone and scene references. Its editor menu registration was
removed while investigating the editor menu-rebuild crash. It does not run
automatically. Do not run full imports or builds on this laptop without approval.

`IntegrationBuild.VerifyAndBuild` runs those checks and makes a development
Linux build at `/tmp/getaway-integration-build/Getaway.x86_64`. Development builds
support the opt-in `--getaway-smoke=host`, `--getaway-smoke=client`, and
`--getaway-smoke=third` arguments. The first two drive an automated network run;
the third verifies rejection when the session is occupied or in progress.
These flags are excluded from release builds.

Before shipping, playtest both role assignments on two machines, disconnect and
reconnect each player, play through a wreck/rematch, and test real controllers.

## GDD alignment and current design choices

The title is **Blind Getaway**. Normal multiplayer gives the driver a normal
road view and gives only the shooter the navigation panel. Players communicate
route instructions verbally; built-in voice chat is not provided. Solo debug
shows only the active role's information and uses the same host simulation.

Five 500 m district stages repeat in endless survival (the user's selected
mode, replacing the document's finite five-city victory). Each stage has an
upcoming left/right/straight route instruction. The road center shifts during
turns; steering must follow it. Running onto the shoulder damages the truck.
Speed and spawn frequency increase with elapsed play. This is a route prototype,
not five individually authored city maps or intersection choices.

Abrupt steering pushes the shooter across the rear platform. A/D or controller tilt
counteracts drift; lingering near an edge drains balance, while the center
restores it. Crossing the roof edge or exhausting balance ends the run as
**FellOff**. The host decides the balance and fall outcome.

The shooter has a 12-round magazine and 72 reserve rounds. R reloads in 1.8 s;
firing with an empty magazine also starts a reload for custom-controller use.
Green supply packs restore one hull and add 24 reserve rounds, capped at 120.

Police near the truck build capture pressure and attack every three seconds.
Destroying police lowers pressure and increments the police-defeated count.
Runs end as **TruckDestroyed**, **Captured**, or **FellOff**. Results include
survival distance, score and police defeated. There is no recorded comedy replay.
Settings include local volume, steering sensitivity and graphics quality.

`PetrusRoadLayout.prefab` flattens the existing base road and complete first
building variant into one render source, preserving authored child transforms
and material references. Runtime render copies use one uniform 0.4 world-unit
conversion instead of fitting each model to a different box. Source root rotation
is preserved; the artificial truck roof, replacement gun and generated city
blocks were removed. Existing truck, shooter, gun, traffic and road art remain.
Animated and physics presentation still needs a manual Editor playtest.

Latest validation: isolated C# compilation of integration runtime scripts and
static scene/prefab-reference checks only. Unity import, RPC code generation,
visual checks and network/hardware playtests have not been rerun.

## First-person perspectives

Both normal multiplayer roles and solo debug use the same first-person camera
path. Driver view is attached to the truck seat and offers normal road visibility;
only shooter view shows route directions, balance, ammunition and the reticle.
Switching in solo debug does not change network role ownership; it selects which
role's controls, camera and HUD are presented on the one local machine.
Mouse is captured only during active play with focus and settings closed.

`IntegratedWorld.DriverEye` and `ShooterEye` expose eye offsets in the Inspector.
The shooter eye follows the replicated roof position. These offsets must still
be visually checked against the existing models; the original truck does not
provide a dedicated cockpit interior. Render geometry containing the driver
camera is hidden locally, as is the shooter's own body in shooter view (the
existing gun remains visible). This is not a newly authored cockpit or FPS arms.

## Truck model correction

The shared `Assets/Prefabs/Player/TruckPrefab.prefab` now uses `Truck ver 1.fbx`
and `kemudi.fbx`, saved through the connected Unity Pipeline plugin. The old
`car_truck5` instance was removed. `kemudi` is a cabin/interior assembly, not only
a steering-wheel mesh; 41 overlapping body renderers were disabled to avoid
drawing matching geometry twice. The two models share a uniform scale and
mount aligned to the existing collision footprint; existing physics, scripts
and platform collider are retained. Driver-eye defaults now follow this cabin,
and shooter/muzzle height now follows the rear cargo deck (1.732 world units).
Live Unity verification confirmed both new model dependencies, no old model
dependency, and zero missing prefab instances. Visual seat alignment and Play
mode still need checking; no Play session or build was started for this repair.

## Current movement and cargo-bed placement

The experimental Rigidbody/local-physics-scene gameplay was removed at the user's
request. Driving, inertia, obstacle checks and hit detection again use the prior
host-owned scripted simulation. No isolated PhysicsScene or dynamic shooter is
created by the integration.

The shooter now stands on the rear cargo bed, not the former rooftop platform.
`DeckHeight` is 1.732 integration units and `RearShooterSpawn` is `(0, 1.732, 0.65)`.
The prefab marker uses `(0, 4.33, 1.625)` before the uniform 0.4 conversion.
The character is fitted uniformly to 1.6 m height and its mesh bounds are shifted
so its feet meet the spawn point. The muzzle and first-person camera follow this
new rear position. Existing scripted balance and falling remain.

The bounds-based rule that hid entire cab meshes from inside the truck was
removed. Exterior truck rendering stays enabled in both first-person views and
Scene view. Only the local player avatar is hidden for first-person viewing.
Cabin/bed positioning still needs a visual Play-mode check; no new Play session
or build was launched for this correction.

## Exterior/interior views and current robber position

The prefab's Truck ver 1 body now has all 74 renderers enabled, while the 62
kemudi renderers default to disabled. Render-only copies include both complete
models and apply local `TruckViewPart` visibility per camera. Robber view and
lobby preview show the complete Truck ver 1 body with kemudi hidden. Driver
view enables kemudi and suppresses only body meshes duplicating that interior.
This restores the original body's cab meshes when switching to the robber.

The robber now stands at the FRONT of the cargo bed, behind the cabin, rather
than near the tailgate. The existing RearShooterSpawn marker name is retained,
but its integration position is `(0, 1.732, 0.65)`. Its source-prefab position is
`(0, 4.33, 1.625)` before the uniform 0.4 unit conversion. The shooter eye offset
is 1.5 m above the feet, inside the 1.6 m avatar height. No Rigidbody gameplay
was reintroduced. Compilation and prefab saving were verified; visual playtesting
is still pending.

## Driver cab, steering wheel and model-matched collision update

Driver view now retains ALL Truck ver 1 exterior meshes. Its local material
copies disable back-face culling so the thin cab panels render from inside.
Only duplicated kemudi panels are suppressed; its unique steering/dashboard
parts remain driver-only. Robber view still hides kemudi and sees the full
Truck ver 1 body. Original materials are not modified. The driver character
avatar is no longer created; DriverSeat/DriverEyeMount remain camera markers.

The kemudi steering wheel uses its actual pPipe1 rim, pCube1 spokes and
pCylinder23 hub on a runtime pivot at the rim's bounds center. Replicated
steering input turns that pivot by up to 180 degrees with smooth return.

Scripted collision uses the measured truck bounds: center `(0, 1.893, -2)` and
size `(5.312, 3.786, 12)` in integration units. Swept relative segment checks
account for both forward travel and lateral steering, including the rear
overhang. Road-edge checks include the vehicle's half-width, and traffic lanes
are spaced 5.2 m apart so a centered truck clears cars in the neighboring lane.
This remains the restored scripted simulation, not Rigidbody gameplay.

The robber stays just behind the cab at `(0, 1.732, 0.65)`, but its proportional
character height is now 2.4 m and eye offset 2.2 m, placing the camera above the
cab roof. Weapon height is 2.1 m above the deck. These changes still need visual
playtesting; no Play session/build was started automatically.
# Windshield visibility

The `Truck ver 1/Plano` mesh uses a runtime transparent glass material so the driver can see the road. The rest of the cab remains opaque and double-sided. Source FBX materials are unchanged. Verified with a small edit-mode render from the driver eye position; no Play session or build is needed for this check.

## Full-screen integration UI

The integration menus use the original `UI Button` scene background with full-screen anchors and a 1280x720 Canvas Scaler. Host, join, role selection, results, settings, and the HUD share cyan text and the original purple button palette. The old green action toolbar and mixed IMGUI menus are replaced in this scene.

`Prefabs/GetawayButton.prefab` is the reusable action button. Change its `GetawayButtonGraphic` gradient, outline, and label to adjust new menu actions together. The graphic draws rounded geometry without baked-in wording, allowing any action label. Existing source buttons receive that same style while preserving their hierarchy. Buttons retain keyboard navigation, disabled states, and pointer feedback.

Settings still expose keyboard/custom controller selection, serial port connection, calibration, steering range/inversion/sensitivity, volume, graphics, and leaving the session. Multiplayer keeps running while settings are open. Solo Debug still switches the active first-person role with Tab; only the shooter sees navigation details.

The steering wheel's visual rotation sign is corrected: right input moves the top of the rim to the driver's right (clockwise), and left input counterclockwise. Gameplay steering is unchanged.

Validation: clean C# and live Editor compilation; edit-mode previews of the main menu at 1280x720 and 1024x768, plus join, lobby, settings, and HUD; all 21 active menu buttons have shared graphics and callbacks; one EventSystem; wheel orientation verified against the source mesh. No Play session, build, or second Editor was launched.
