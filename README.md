Engine : Unity 6.6 (6000.6.3f1)

Additional Package : 
- Cinemachine (6.6.0)

## Blind Getaway integration

Open `Assets/Scenes/IntegratedGetaway.unity` for the new two-player integration.
Host/join on LAN, choose driver and shooter, both ready up, then start survival.
The first polished daylight Downtown district repeats along a baked 1,800 m curved
route. Shooter-only bend warnings, balance/falling, ammunition/reloading, and
police pressure follow the GDD in endless-survival mode. Four additional districts
are planned; the HUD currently reports the one implemented district.
Keyboard/mouse testing and the original custom serial controllers are selectable
per player. See [integration controls and testing](Assets/Integration/README.md).

## Cooperative multiplayer getaway

Open `Assets/Scenes/CoopGetaway.unity` and press Play. This is the earlier
prototype scene. The playable prototype uses Unity Netcode and
Unity Transport, with a simple generated car, roof robber, and highway.
The older Photon and local test scenes are separate experiments.

1. Run two game instances (a build plus the Editor works). On one, enter a name
   and select **Host Lobby**. On the other, enter the host computer's LAN IPv4
   address and select **Join Lobby**. Use `127.0.0.1` on the same computer.
2. Choose **Driver** on one instance and **Robber** on the other. Each role has
   one slot; either player can take either role. Both select **Ready**, then the
   host selects **Start Getaway**.
3. Driver: **A/D** or **left/right arrows** steer the automatically moving car.
   Robber: **A/D** or **left/right arrows** move across the roof; move the mouse
   horizontally to aim, and hold **left click** or **Space** to shoot roadblocks.
4. Avoid or shoot red roadblocks. Three collisions wreck the car. Reach 1,500 m
   to escape together. The host can return both players to the lobby for a rematch.

This is direct-IP LAN multiplayer on UDP port **7777**. Allow that port through
the host firewall. Internet matchmaking, Relay, and automatic room discovery
are not included. Gameplay state, role exclusivity, ready checks, shot cooldown,
roadblock hits, damage, and victory are decided by the host. A third player or
an in-progress join is rejected. If a partner leaves, the host returns to the
lobby; if the host leaves, the partner disconnects and can reconnect or host.
Keyboard and mouse are supported without Arduino hardware in this scene.

### Verification

C# compilation against the project's installed Unity/Netcode assemblies passes.
A full Unity import, Netcode RPC code-generation check, build, and two-instance
playtest still need to run in a working Unity Editor. Manual acceptance checks:
join with two clients; reject a third; prevent duplicate roles; prevent starting
until both are ready; test each role with the host and client swapped; verify
shooting and collisions agree on both screens; complete and lose a round; replay;
and disconnect/rejoin from both lobby and gameplay.
