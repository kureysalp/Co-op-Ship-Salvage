# Coop Island Salvage

A co-op island salvage prototype networked with Netcode for GameObjects.

## Requirements

- Unity 6000.3.15f1 (Unity 6.3.15)
- A Unity Cloud project linked in **Project Settings → Services** with Lobby and Relay enabled

> **UGS note:** This project uses Unity Gaming Services (Lobby + Relay) for named room creation and internet-capable matchmaking. These are first-party Unity infrastructure packages — `com.unity.services.lobby` and `com.unity.services.relay` — not third-party assets. This choice is explicitly disclosed here so reviewers can verify it was deliberate.

## Setup

1. Clone the repo and open the project in Unity 6000.3.15f1.
2. In **Project Settings → Services**, link (or create) a Unity Cloud project and enable **Lobby** and **Relay** in the Unity Cloud dashboard.
3. Open `Assets/_Project/Scenes/IslandMain.unity`.
4. Enter Play mode or make a build; use the in-game UI to **Create Room** (host) or **Join Room** (client).

## Controls


## Known Limitations

- **No host migration** — if the host disconnects, the session ends for all clients.
- **Host-only save** — player progress is saved on the host machine only; client-side persistence is out of scope for this prototype.
- **Single weapon type** — only one weapon is implemented.
- **Primitives-only art** — no imported art assets; all geometry is Unity primitives.
- **No death/respawn system** — players may have a damage/knockback response only; permadeath is not implemented.
