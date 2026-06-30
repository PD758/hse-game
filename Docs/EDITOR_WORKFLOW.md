# Editor workflow

This project is meant to be opened and played from the Unity Editor.

## First setup

1. Open the project in Unity `6000.5.1f1`.
2. Wait until the Asset Database finishes importing.
3. Run `Rogue > Bootstrap All Scenes`.
4. Run `Rogue > Use Main Menu As Play Start`.
5. Save the project and scenes.

After that, pressing Play should start from `Assets/Scenes/MainMenu.unity`.
The runtime scripts expect the scenes to be baked. If a scene is missing baked objects,
the component logs an error instead of generating objects during Play Mode.

## Avoiding import work on Play

Unity imports assets and recompiles scripts before Play Mode when files changed on disk.
That cannot be removed safely. The practical fix is to keep the project clean before
pressing Play:

- wait for the spinner in the bottom-right corner to finish;
- do not edit scripts while testing gameplay;
- do not regenerate scenes or reimport textures before every Play;
- keep generated/imported assets saved in `Assets`, not created during gameplay.

If Unity still refreshes on every Play, check whether an editor script is changing assets
or project settings automatically.

## Editor-first structure

`Rogue > Bootstrap All Scenes` is intentionally idempotent: it creates missing scenes,
updates project scene settings, and leaves already-baked scenes untouched. Use
`Rogue > Force Rebuild All Scenes` only when you intentionally want to regenerate
`Assets/Generated` and the baked scene objects.

The force rebuild command bakes generated art into `Assets/Generated` and writes scene
objects into `Assets/Scenes`. Runtime scripts bind those existing objects by name and
serialized references.

For an even more conventional Unity project structure, the next step is to replace the
scene-object grid with authored Tilemap assets:

- put sliced sprites in `Assets/Sprites`;
- put reusable entities in `Assets/Prefabs`;
- build the level with Tilemap or scene GameObjects;
- keep scene references serialized in MonoBehaviours with `[SerializeField]`;
- let runtime scripts control state, input, movement, damage, and scene transitions only.

This makes the game inspectable from Hierarchy and Inspector instead of being hidden in
large procedural setup methods.
