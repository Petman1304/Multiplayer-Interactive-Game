# Downtown environment: authoring and acceptance

This milestone implements one daylight district. Four additional district themes
remain future content. `DowntownRoute.asset` and `DowntownEnvironment.asset` are
referenced by the integrated scene's `IntegratedWorld`; `UseDowntownRoute` is on.
Turning it off retains the previous straight-road presentation for comparison.

## Route contract

`RouteDefinition.Sample(totalDistance)` returns a double-precision position,
heading/right vector, and curvature. The Editor bakes authored Beziers into
at-most-one-metre arc-length samples. The district length is approximately
1,800.003 m; the smallest measured bend radius is 126.7 m. Four eased 45-degree
bends return to the original heading. Repetition composes the endpoint transform,
including for negative distances used behind the start. The district is an
extending corridor, not a closed loop returning to its starting building.

`RoundState.Distance` is total route progress and `TruckX` is lateral displacement.
`RoadThreat.RouteDistance` is absolute progress; `LaneOffset` is lateral position.
The replicated RouteSeed selects repeatable encounter/visual variants. Legacy X/Z
fields and the old 500 m formulas are only used by the explicit rollback mode.
The v3 handshake rejects older integration clients; use matching asset builds.

All authority geometry and the reticle sample the route. Shots transform into
oriented threat-box space; collisions test both truck and threat box axes and
substep movement at most 0.5 m or heading change at most 2 degrees. A double-precision
presentation origin keeps the truck near zero without changing simulated progress.
Tracers retain their firing origin distance and move with that presentation origin.

Curve drift is speed times curvature times an editable 18 m coefficient. The
maximum measured drift at 42 m/s is 5.97 m/s, below the 8 m/s steering authority.
Curves produce bounded outward shooter velocity, while steering changes produce
transient impulses. Road-edge limits reserve space for the rear overhang's curved
sweep and a small curb margin. This remains arcade/scripted movement.

## Layout, art, and encounters

The recipe makes ten 100 m straight sections and four 200 m bends. Six module
variants provide boulevard A/B, left/right bend frontage, construction, and a
municipal-square clock-tower landmark. Road width is 16 m; lanes are spaced 5.2 m.

Buildings are rendering copies of named Petrus building groups, uniformly converted
at 0.4 units and positioned along route tangents. They are not deformed. The curve
sweeps only road, ground, sidewalk, curb-top, and marking meshes. Lamp housings and
construction furniture are decorative and contain no lights or colliders. Buildings keep their complete source geometry throughout the visible range; plain
box replacement LODs have been removed because they hid facades too early. The
bounded tile pool, camera far clip, and fog limit scenery coverage. Small props
disappear past 120 route metres. Original imported meshes and materials are unchanged.

Seven prewarmed 100 m tiles cover the requested 350 m forward / 80 m rear window
with recycling slack. Each tile owns a fixed road mesh buffer and prewarmed module
variants. Road vertices update only on recycle; transforms update with the origin.
No section or building is instantiated while the pool is presenting a run.

Encounter anchors at 200, 430, 690, 900, 980, 1050, 1190, and 1700 m target
slow traffic, a blocked lane, lane-changing traffic, recovery, rear police,
light traffic, staggered barriers, and recovery. The host schedules complete waves
within budgets of four/six/eight threats by circuit; the fixed pool has 18 slots.
It reserves lane-change corridors and validates moving occupancy over a 36 s
horizon at constant speed, normal acceleration, and maximum supported acceleration.
Warning lead intends six seconds at expected speed and reserves at least four
seconds even under maximum acceleration. Quiet zones and active pursuit suppress
conflicting demands. Unsafe or late waves are deferred or skipped, never forced. Lane choices are
seeded; the legacy `Lane` field is not an override. `SpeedFraction`, `SignalSeconds`
and `ChangeSeconds` use defaults 0.65/2/3 when zero. The lane-change pattern starts
in the centre and chooses one adjacent destination.

The conservative checker projects swept oriented boxes into each sampled route
frame and searches reachable lateral positions, including footprint, steering
allowance and road-edge clearance. This is a bounded admission check, not a proof
against arbitrary player inputs, all future combinations, or unmeasured network
conditions. See the integration README for pursuit rules and manual acceptance.

## Editing and rebuilding

Author through the existing Editor/Pipeline, never by modifying FBX internals.
These explicit C# entry points are available without menu registration:

- `DowntownRouteAuthoring.Create()` regenerates the default recipe and baked asset.
- `DowntownRouteAuthoring.Bake(route)` rebakes edited control points; mark the asset
  dirty and save afterward. Section distances are reconciled with baked length.
- `DowntownArtAuthoring.Create()` rebuilds the six decorative prefabs/materials.
- `DowntownRenderingAuthoring.Create()` rebuilds isolated renderer/quality/sky assets.
- `DowntownVerification.Geometry()`, `.Pool()`, and `.Scene()` run edit-mode checks.
- `LevelVerification.Run()` verifies moving encounters, scheduling, rear pursuit and v3 serialization.
- `DowntownPreview.Render(distance, driver, absolutePngPath, overhead)` creates a
  960x540 still in a temporary preview scene, then restores rendering settings.

Create operations overwrite their generated assets. Change the authoring recipe
for persistent regenerated changes, or edit a generated prefab and do not regenerate
it afterward. Keep additional districts as separate route/environment assets.
Road data must have connected endpoints and matching tangents; all clients need the
same baked route. Validate seams and encounter corridors before enabling new content.

## Rendering and manual performance capture

The scene uses daylight sky, one sun, tri-colour ambient lighting, and linear fog
from 160 to 300 m. Camera far clip is 380 m. The integration caps rendering at 60 FPS and restores
the prior frame cap on unload. The integration's forward renderer has
no renderer features. There is no SSAO, HDR, motion blur, depth of field, realtime
reflection update, or baked lighting step. Pipeline/light/fog/sky overrides restore
on scene unload; existing PC/Mobile pipeline assets remain unchanged.

| Preset | Main shadows | Cascades | Distance | MSAA |
| --- | --- | --- | --- | --- |
| Low (default) | 1024, hard | 2 | 40 m | Off |
| Balanced | 2048, soft | 2 | 70 m | 2x |

Additional-light shadows and additional realtime lighting are off in both presets.
Target acceptance is 60 FPS at 1280x720 on Low on the current laptop. It has **not**
been measured; available RAM was low and swap full during implementation.

When ready, manually start a game and press **F8**. The opt-in recorder writes one
CSV row per second for 600 seconds under `Application.persistentDataPath`; the exact
path is logged. F8 stops early. Columns include phase, distance, FPS, CPU main-thread
milliseconds, GPU milliseconds, managed/native allocation totals, active tile count,
preset, resolution, local menu visibility, and role. CPU timing falls back to the Main Thread ProfilerRecorder counter.
GPU timing requires Frame Timing Stats and a supported graphics backend; enable
that option when preparing an intentional profiling build. Missing timings are
blank and must not be interpreted as zero cost. Recorder code is absent in release.

Capture Low and Balanced separately. Analyse Playing rows with `menu_open=False` at 1280x720, excluding result
frames. After warm-up, check for a flat memory trend, seven active tiles, no repeated
recycle hitches, and CPU/GPU costs separately. CSV managed allocations are resident
memory totals, not proof that every frame allocates zero bytes. Check GC allocations
in the Unity Profiler separately. Stop if the machine begins swapping heavily.

## Acceptance record

Completed in the existing Editor, without Play/build or another Editor:

- Runtime and Editor code compile with no errors.
- Metre spacing, minimum radius, all major boundaries, and distant-origin seams pass.
- 512 deterministic encounter-authoring cases pass. Moving-wave admission has a separate
  480-candidate check: 404 reachable / 76 rejected, with all pattern types represented.
- Forward/lateral sweeps, adjacent-lane clearance, rear overhang, curved target rays,
  reverse-ray misses, and route/network state serialization round-trips pass.
- Binary-keyboard steering at 42 m/s stays inside the road for an entire district;
  the check's conservative controller reached a maximum 2.009 m lateral offset.
- 10,000 route samples allocate zero managed bytes after warm-up.
- An 18 km recycling check retains seven tiles and exactly 1,373 environment transforms after proxy removal.
- Driver/shooter stills confirm the road is visible, cab remains present, shooter
  sees over the roof, and the overhead road has continuous curves.
- Scene references, one EventSystem, and isolated Low/Balanced settings pass.

Still required manually before declaring the performance/multiplayer acceptance met:

1. Solo driving through every bend; Tab switch, aim/fire/reload, wreck and rematch.
2. Two real clients with each role hosted in turn; collision, shot, encounter, route
   seed, disconnect/rejoin, and previous-protocol rejection agreement.
3. Both physical serial controllers, including unplug/reconnect and calibration.
4. Ten-minute Low and Balanced captures after resolving memory pressure; verify FPS,
   CPU/GPU frame times, GC allocations, and sustained memory stability.

## Building visibility correction

Removed screen-relative box LODs from all 36 frontage groups in the six modules,
and from the authoring recipe. Serialized asset checks confirm all six original
artwork groups per module and every renderer enabled. This removes 252 proxy
transforms from the seven-tile pool (expected total 1,373). The pool count and
updated visual result still need Editor verification: Unity crashed during script
reload in native `ScriptCommands::Rebuild` / `MenuController::GetChecked` before
post-reload previews could run. No second Editor or Play session was launched.

## Orange building surface correction

The orange `Amaryllis_City_Pok_mon_Center1` / `Mesh12` source contains inward-facing
exterior polygons. Four-sided source previews with culling on/off confirmed that
the missing-looking walls and roof are present but culled. The nine integration
placements now share ten integration-only material copies with `_Cull = 0`.
Original FBX meshes/materials remain unchanged. The authoring recipe preserves this
correction when regenerating modules. A driver-view still confirms closed walls;
all nine placements pass material checks. The updated pool check passes with seven
tiles, 1,373 transforms over 18 km, and zero scenery colliders. No Play/build was run.

### Level update acceptance limits

The v3 encounter and rear pursuit implementation passed compilation and pure
edit-mode checks, including actual first-circuit lane-change admission and
scheduler cooldown, quiet-zone, expiration and budget policies. These checks
load isolated compiled code into the existing Editor and create only a temporary
route copy; they do not start gameplay or a second Editor. New presentation
(indicators, siren, rear aiming from the bed) and real two-client agreement still
require the manual checks listed above. Performance acceptance remains unmeasured.
