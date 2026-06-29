# 2D Multiplayer Roguelike Roadmap

## Target

Top-down 2D roguelike with deterministic grid simulation, multiplayer-ready state replication, tactical enemy AI, and room-scale puzzles.

## Vertical Slice 0

- Local single-player grid prototype.
- Turn-based movement on a tile map.
- Enemy AI with patrol, sight, memory, and chase states.
- Handcrafted multi-floor slice with pressure plates, keys, doors, traps, and patrol routes.
- Runtime-generated placeholder pixel visuals.
- NetSync native codec copied into the Unity project for later integration.

## Network Direction

Use Unity as the transport owner. NetSync is a packet codec only:

- client sends input commands;
- host/server owns authoritative simulation;
- host sends snapshots and deltas encoded with NetSync;
- clients render interpolated or corrected state from decoded events.

For the first multiplayer milestone, keep the simulation deterministic and compact:

- entity id;
- prefab id;
- grid position;
- integer stats;
- flags;
- rare command payloads.

## Next Milestones

1. Split the prototype into pure simulation and Unity presentation.
2. Add Unity Transport for local host/client play.
3. Encode snapshots with NetSync and decode them on a second local client.
4. Replace runtime placeholder sprites with a small tileset.
5. Expand AI into utility scoring: noise, scent/tracks, group calls, flanking.
6. Add puzzle room generator templates.
