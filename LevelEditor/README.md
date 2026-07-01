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
- Left mouse on sidebar: select palette item
- `x`: select exit tool
- Left mouse: paint/place/select
- Left mouse on an empty cell with a selected object/enemy: move it
- Right mouse drag: pan
- Middle mouse or `Space` + drag: pan
- Mouse wheel: zoom
- `+/-`: zoom
- Cursor tool + left drag entity: move player start, objects, enemies, or exits
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

- Gate: `id`, `group`, `requiresPlates`, `requiresStories`, `requiresEnemies`, vertical/horizontal frame
- Plate: `group`
- Camera: drag the arrow handle on the map to rotate
- Story: `id`, text
- Exit: `id`, `targetLevel`, `requiresGate`, branch
- Enemy: `id`, `level`, branch and patrol controls
- Player start / objects / exits / enemies: use Cursor drag to move

## Validation

The inspector shows validation errors and warnings. Red cells are errors, yellow cells are warnings.
Click a validation row to select the related object when available.
