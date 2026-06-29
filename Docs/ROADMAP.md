# 2D Single-Player TV Roguelike Roadmap

## Target

Top-down 2D roguelike about a viewer pulled into mandatory television channels, with responsive real-time movement, tactical enemy AI, room-scale puzzles, and a hidden psychological branch between aggression and clarity.

## Vertical Slice 0

- Local single-player prototype.
- Smooth real-time top-down movement with collision, acceleration, and inertia.
- Enemy AI with patrol, sight, memory, and chase states.
- Intro cutscene and first handcrafted channel: surreal television news.
- Viewer rating that falls during inactivity, accelerates over time, and damages the player at critical values.
- Seamless first-channel branch: puzzle/story path versus combat/aggression path, with the unchosen passage collapsing.
- Hidden narrative metrics for aggression and clarity that will later branch into two endings.
- Runtime-generated placeholder pixel visuals.

## Systems Direction

Keep the prototype single-player and make the world react to how the player watches the channel:

- clarity opens colder, quieter, more readable rooms;
- aggression increases red noise, enemy pressure, and hostile feedback;
- viewer rating is the immediate survival pressure that keeps the run moving;
- branch choices are made spatially in connected rooms, not through menus.

## Next Milestones

1. Split the prototype into cleaner simulation, presentation, and level data.
2. Replace runtime placeholder sprites with a small news-channel tileset.
3. Expand AI into utility scoring: noise, line of sight, group calls, flanking.
4. Add a second handcrafted channel with a distinct visual language and puzzle grammar.
5. Add persistence for run-level aggression/clarity and prototype two ending stubs.
6. Add room templates after the handcrafted vertical slice feels good.
