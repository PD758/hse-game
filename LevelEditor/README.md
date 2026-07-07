# Level Editor

Simple pygame editor for JSON levels in `Assets/Levels`.

## Run

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r LevelEditor/requirements.txt
python LevelEditor/editor.py Assets/Levels/prototype_01.json
python LevelEditor/editor.py --scale-ui 2 --cell-size 48 Assets/Levels/prototype_01.json
python LevelEditor/editor.py Assets/Levels/prototype_02.json --new
```

## Controls

- `1..9/0`: select palette item
- `c`: cursor/select mode
- `g`: regions mode
- Left mouse on sidebar: select palette item
- `x`: select exit tool
- `f`: select flashlight object
- `t`: select arbitrary texture tool
- `i`: select light tool
- Left mouse: paint/place/select
- Left mouse on an empty cell with a selected object/enemy: move it
- Right mouse drag: pan
- Middle mouse or `Space` + drag: pan
- Mouse wheel: zoom
- `+/-`: zoom
- Cursor tool + left drag entity: move player start, objects, enemies, or exits
- Regions tool: select a region, click cells to toggle them, drag to add cells
- `P`: toggle patrol edit mode for selected enemy
- Drag selected camera arrow handle: rotate direction
- `Alt` + left drag selected camera cell: rotate direction
- `Q/E`: rotate selected directional object by 45 degrees fallback
- `Shift+Q/E`: rotate selected directional object by 15 degrees fallback
- `R`: toggle selected gate vertical/horizontal frame
- `V`: toggle validation overlay
- `L`: toggle logic overlay
- `Delete`: delete selected object/enemy
- `Ctrl+Z`: undo
- `Ctrl+S` or sidebar `Save`: save

## Inspector

The right panel shows editable properties for the current selection.

- Gate: `id`, `group`, `requiresPlates`, `requiresStories`, `requiresEnemies`, `requiresStats`, vertical/horizontal frame
- Gate stat conditions use `op stat value`, separated by semicolons, e.g. `ge enemiesKilledOnLevel 2; ge camerasTriggered 1; lt currentRating 50`
- Plate: `group`
- Remote / Flashlight / Heal: pickup objects for the ability and recovery loops
- Camera: drag the arrow handle on the map to rotate
- Text Note: `id`, `text`
- Image Note: `id`, `imagePath`; use repo-relative `Assets/Resources/...` paths or resource keys like `Notes/note_01`
- Exit: `id`, `targetLevel`, `requiresGate`; `branch` is legacy metadata and ignored by gameplay
- Enemy: `id`, `level`, `hp`, branch metadata and patrol controls
- Texture: `texturePath`, `scale`, `rotation`, `sortingOrder`, `castsShadow`; use repo-relative paths under `Assets/Resources/...` for Unity builds
- Light: `type` (`point`/`cone`), `color` as `#RRGGBB`, `intensity`, `radius`, cone angles/rotation
- Region: `id`, cell count, clear/delete controls
- Event actions are edited as rows. Supported action types: `fallStone`, `spawnEnemy`, `setTile`, `spawnObject`, `removeObject`, `playEffect`.
- For `enterRegion` events, `+ stones in region` adds one `fallStone` action for each cell in the selected region.
- Player start / objects / exits / enemies: use Cursor drag to move
- Textures / lights: use Cursor drag to move freely; hold `Shift` while dragging or placing to snap to cell centers

## Validation

The inspector shows validation errors and warnings. Red cells are errors, yellow cells are warnings.
Click a validation row to select the related object when available.
