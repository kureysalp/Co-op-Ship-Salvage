# Coop Island Salvage

A host-authoritative, drop-in co-op prototype built in Unity 6 with **Netcode for GameObjects (NGO)**. Players host or join a LAN session, sail a shared boat, fight AI-crewed enemy ships and guards, and salvage loot into a networked inventory.

Built as a time-boxed case prototype (3-day scope). This README describes the build **as it actually ships** — see [Scope & Known Limitations](#scope--known-limitations) for what was intentionally cut.

## Highlights

- **Host-authoritative netcode** — the host owns all simulation. Boats, AI ships, crew, and loot are server-owned; the server validates every gameplay action via `ServerRpc`.
- **Client-side prediction** — the first-person controller uses `AnticipatedNetworkTransform` for responsive local movement with server reconciliation and smoothing for remote players.
- **Shared, physics-driven boats** — server-authoritative rigidbody boats with wheel steering. Passengers and riders are carried correctly through the boat's position/rotation deltas, so players and objects stay put on a moving deck.
- **AI enemy ships & crew** — enemy vessels are assembled at runtime (`EnemyShipBuilder`), piloted by AI (`AiBoatPilot`), and crewed by behavior-tree-driven guards (`EnemyCrew` + `BehaviorTree`) that engage players.
- **Networked loot & inventory** — world items are picked up via a raycast interaction system into a per-player networked inventory with a slot-based UI.
- **Single-player friendly** — the host is a client locally, so a solo host session works with zero connected peers.

## Requirements

- Unity **6000.3.15f1** (Unity 6.3.15)
- No external services or accounts required — networking is peer-to-peer over LAN using the default Unity Transport (UTP).

## Running It

### In the Editor

1. Clone the repo and open the project in Unity **6000.3.15f1**.
2. Open `Assets/_Project/Scenes/IslandMain.unity`.
3. Enter Play mode and use the session UI:
   - **Create** — starts a host on `0.0.0.0:7777` and displays the host's local IP.
   - **Join** — connects a client to the host.

### Host + client on a LAN

- **Host:** click **Create**. Share the displayed `local-ip:7777` with the other player.
- **Client:** enter the host's IP in the address field and click **Join**.
- Both machines must be on the same network with UDP port `7777` reachable.

> **Note on Join:** the in-editor Join button currently defaults to `127.0.0.1` for fast local same-machine / ParrelSync testing. LAN join uses the IP entered in the address field. See [Scope & Known Limitations](#scope--known-limitations).

## Controls

On foot:

| Action | Input |
|---|---|
| Move | WASD |
| Look | Mouse |
| Sprint | Left Shift |
| Jump | Space |
| Interact | Context menu — `[E]` / `[Q]` / `[F]` / `[G]` |
| Fire | Left Mouse Button |
| Reload | R |
| Toggle inventory | Tab |

Interaction is context-sensitive: aim at a boat wheel, loot item, or other interactable and an on-screen prompt shows the available options (e.g. `[E] Pilot Ship`, `[E] Pick up`).

Piloting a boat:

| Action | Input |
|---|---|
| Throttle / Steer | WASD |
| Leave the wheel | `[E]` |

Walk onto a boat to board as a passenger; aim at the wheel and interact to take the helm.

## Gameplay Loop

1. Host or join a session and spawn on the island.
2. Board a boat and pilot it out to sea (one player steers, others ride along).
3. Intercept AI-crewed enemy ships; fight their guards.
4. Salvage loot into your inventory.
5. On death you respawn at the island spawn point with health restored (knockback/respawn model — see limitations).

## Architecture

- **Networking:** Host-authoritative NGO over UTP (direct-IP LAN). No dedicated server build.
- **Scene:** Single scene — `IslandMain`.
- **Ownership:** Player objects are owned by their connecting client (`SpawnAsPlayerObject`). Boats, AI ships, and loot are server-owned; clients drive them only through validated RPCs.
- **Movement:** Owner-predicted first-person controller reconciled against the server via `AnticipatedNetworkTransform`; remote players are smoothed.
- **Packages:** Unity-official only — NGO, UTP, Input System. No third-party assets.

### Project Structure

```
Assets/
  _Project/
    Scripts/
      Core/     # NetworkManager bootstrap, room/session, player registry, spawns
      Player/   # FPS controller, interaction, equipment, inventory
      Boats/    # boat controller, pilot/rider system
      Ships/    # AI ship assembly & spawning
      AI/        # enemy crew, AI gun, behavior tree
      Game/      # items, item database, pickups
      Loot/
      UI/        # session UI, inventory UI, interaction menu
      Utils/     # health, damage, VFX pooling, weapon system
    Prefabs/
    Scenes/
      IslandMain.unity
    Art/         # primitives only
  Settings/      # URP assets, Input Actions
```

## Scope & Known Limitations

This is a deliberately time-boxed prototype. The following were cut or simplified, and are called out here rather than hidden:

- **LAN direct-IP only** — sessions connect by IP over UTP. There is no online matchmaking, lobby service, or relay; internet play would require port forwarding or a relay layer. The Join button defaults to `127.0.0.1` for local testing; enter the host IP for LAN play.
- **No host migration** — if the host disconnects, the session ends for all clients.
- **No save/persistence** — game state is in-memory only; nothing is written to disk.
- **No dedicated server** — the host is also a player.
- **No client-side authority / anti-cheat** — the host is fully trusted.
- **Single weapon type.**
- **Primitives-only art, no animation** — all geometry is Unity primitives.
- **Simplified death model** — players respawn at the island spawn with health restored; there is no full death/knockback/scoring system.
