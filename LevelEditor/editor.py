from __future__ import annotations

import argparse
import copy
import math
import sys
from pathlib import Path

import pygame

from inspector import InspectorState, draw_inspector, handle_click as handle_inspector_click, handle_key as handle_inspector_key, point_inside as inspector_contains
from level_io import expand_tile_variants, expand_tiles, load_level, save_level
from palette import TOOLS, tool_by_key
from schema import ENEMY_TYPE, object_at, default_level
from sprites import SpriteBank
from ui import draw_sidebar, draw_top_status, save_button_at, sidebar_width, tool_at, top_bar_height
from validation import validate_level
from viewport import Viewport


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def default_level_path() -> Path:
    return repo_root() / "Assets" / "Levels" / "prototype_01.json"


def main() -> int:
    args = parse_args()
    ui_scale = max(0.75, min(3.0, args.scale_ui))
    level_path = args.level_path.resolve() if args.level_path is not None else default_level_path()
    level = new_level(level_path) if args.new else load_level(level_path)
    tiles = expand_tiles(level)
    tile_variants = expand_tile_variants(level)
    size = level["size"]
    width = int(size["width"])
    height = int(size["height"])

    pygame.init()
    pygame.key.set_repeat(360, 38)
    screen = pygame.display.set_mode((1280, 820), pygame.RESIZABLE)
    pygame.display.set_caption(f"Level Editor - {level_path.name}")
    font = pygame.font.SysFont("monospace", round(15 * ui_scale))
    clock = pygame.time.Clock()

    sprites = SpriteBank(repo_root())
    viewport = Viewport(width, height, args.cell_size)
    viewport.offset.x = 20 + sidebar_width(ui_scale)
    viewport.offset.y = top_bar_height(ui_scale) + 16
    inspector_state = InspectorState()
    selected_tool = 0
    selected_ref: tuple[str, int] | None = None
    selected_cell: tuple[int, int] | None = None
    current_tile_variant = -1
    dirty = False
    patrol_mode = False
    panning = False
    paint_snapshot_pushed = False
    angle_drag = False
    drag_ref: tuple[str, int] | None = None
    drag_last_cell: tuple[int, int] | None = None
    drag_free = False
    show_validation = True
    show_logic = False
    last_mouse = (0, 0)
    undo_stack: list[dict] = []

    running = True
    while running:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False
            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_z and pygame.key.get_mods() & pygame.KMOD_CTRL:
                    restored = pop_undo(undo_stack)
                    if restored is not None:
                        level = restored["level"]
                        tiles = restored["tiles"]
                        tile_variants = restored["tile_variants"]
                        selected_ref = restored["selected_ref"]
                        selected_cell = restored["selected_cell"]
                        current_tile_variant = restored["current_tile_variant"]
                        patrol_mode = restored["patrol_mode"]
                        inspector_state.clear_text()
                        dirty = True
                    continue
                if inspector_state.active_field:
                    push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                    changed = handle_inspector_key(inspector_state, level, selected_ref, event)
                    discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                    dirty = changed or dirty
                    continue
                if event.key == pygame.K_s and pygame.key.get_mods() & pygame.KMOD_CTRL:
                    save_level(level_path, level, tiles, tile_variants)
                    dirty = False
                elif event.key == pygame.K_DELETE and selected_ref is not None:
                    push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                    delete_selected(level, selected_ref)
                    selected_ref = None
                    inspector_state.clear_text()
                    dirty = True
                elif event.key == pygame.K_p:
                    patrol_mode = not patrol_mode
                elif event.key == pygame.K_v:
                    show_validation = not show_validation
                elif event.key == pygame.K_l:
                    show_logic = not show_logic
                elif event.key in (pygame.K_EQUALS, pygame.K_PLUS, pygame.K_KP_PLUS):
                    viewport.zoom(1, pygame.mouse.get_pos())
                elif event.key in (pygame.K_MINUS, pygame.K_KP_MINUS):
                    viewport.zoom(-1, pygame.mouse.get_pos())
                elif event.key in (pygame.K_q, pygame.K_e):
                    step = 15 if pygame.key.get_mods() & pygame.KMOD_SHIFT else 45
                    if event.key == pygame.K_q:
                        step = -step
                    push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                    changed = rotate_selected(level, selected_ref, step)
                    discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                    dirty = changed or dirty
                elif event.key == pygame.K_r:
                    push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                    changed = toggle_selected_frame(level, selected_ref)
                    discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                    dirty = changed or dirty
                else:
                    choice = tool_by_key(event.unicode)
                    if choice is not None:
                        selected_tool = choice
                        selected_ref = None
                        selected_cell = None
                        patrol_mode = False
            elif event.type == pygame.MOUSEWHEEL:
                mouse_pos = pygame.mouse.get_pos()
                if inspector_contains(screen, mouse_pos, ui_scale):
                    inspector_state.scroll(event.y, screen.get_height(), ui_scale)
                else:
                    viewport.zoom(event.y, mouse_pos)
            elif event.type == pygame.MOUSEBUTTONDOWN:
                last_mouse = event.pos
                if event.button == 3:
                    panning = True
                elif event.button == 2:
                    panning = True
                elif event.button == 1:
                    if pygame.key.get_pressed()[pygame.K_SPACE]:
                        continue
                    if inspector_contains(screen, event.pos, ui_scale):
                        push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                        changed, current_tile_variant = handle_inspector_click(inspector_state, level, selected_ref, selected_cell, tile_variants, current_tile_variant, font, ui_scale, event.pos)
                        if inspector_state.pending_select_ref is not None:
                            selected_ref = inspector_state.pending_select_ref
                            selected_cell = None
                            inspector_state.pending_select_ref = None
                            inspector_state.reset_scroll()
                        discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                        dirty = changed or dirty
                        continue
                    if save_button_at(event.pos, ui_scale, screen.get_height()):
                        save_level(level_path, level, tiles, tile_variants)
                        dirty = False
                        continue
                    sidebar_tool = tool_at(event.pos, ui_scale)
                    if sidebar_tool is not None:
                        selected_tool = sidebar_tool
                        selected_ref = None
                        selected_cell = None
                        inspector_state.clear_text()
                        inspector_state.reset_scroll()
                        patrol_mode = False
                        continue
                    cell = viewport.screen_to_cell(event.pos)
                    if cell is not None:
                        inspector_state.clear_text()
                        if TOOLS[selected_tool]["kind"] == "region":
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            selected_ref, selected_cell, changed = handle_region_click(level, selected_ref, cell)
                            discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                            dirty = changed or dirty
                            continue
                        if TOOLS[selected_tool]["kind"] == "cursor" and not patrol_mode:
                            existing = find_at_screen(level, viewport, event.pos) or find_at(level, cell)
                            if existing is not None:
                                push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                                selected_ref = existing
                                selected_cell = None
                                inspector_state.reset_scroll()
                                drag_ref = existing
                                drag_free = is_free_ref(existing)
                                drag_last_cell = cell
                                continue
                        if TOOLS[selected_tool]["kind"] in ("decoration", "light"):
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            selected_ref = create_free_visual(level, TOOLS[selected_tool]["kind"], viewport, event.pos)
                            selected_cell = None
                            inspector_state.reset_scroll()
                            dirty = True
                            continue
                        if directional_handle_hit(level, viewport, selected_ref, event.pos) or (pygame.key.get_mods() & pygame.KMOD_ALT and selected_directional_object(level, selected_ref) is not None):
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            angle_drag = True
                            dirty = update_direction_from_mouse(level, viewport, selected_ref, event.pos) or dirty
                        else:
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            next_ref, next_cell, changed = handle_left_click(level, tiles, tile_variants, selected_tool, cell, current_tile_variant, patrol_mode, selected_ref)
                            selected_ref = next_ref
                            selected_cell = next_cell
                            inspector_state.reset_scroll()
                            if changed:
                                dirty = True
                                paint_snapshot_pushed = TOOLS[selected_tool]["kind"] == "tile"
                            else:
                                undo_stack.pop()
            elif event.type == pygame.MOUSEBUTTONUP:
                if event.button in (2, 3):
                    panning = False
                if event.button == 1:
                    discard_unchanged_undo(undo_stack, level, tiles, tile_variants)
                    paint_snapshot_pushed = False
                    angle_drag = False
                    drag_ref = None
                    drag_last_cell = None
                    drag_free = False
            elif event.type == pygame.MOUSEMOTION:
                if panning or (pygame.mouse.get_pressed()[0] and pygame.key.get_pressed()[pygame.K_SPACE]):
                    viewport.pan(event.rel)
                elif angle_drag:
                    dirty = update_direction_from_mouse(level, viewport, selected_ref, event.pos) or dirty
                elif drag_ref is not None:
                    if drag_free:
                        move_free_selected(level, drag_ref, viewport, event.pos, bool(pygame.key.get_mods() & pygame.KMOD_SHIFT))
                        selected_ref = drag_ref
                        selected_cell = None
                        dirty = True
                    else:
                        cell = viewport.screen_to_cell(event.pos)
                        if cell is not None and cell != drag_last_cell and can_drop_entity(level, tiles, drag_ref, cell):
                            move_selected(level, drag_ref, cell)
                            selected_ref = drag_ref
                            selected_cell = None
                            drag_last_cell = cell
                            dirty = True
                elif pygame.mouse.get_pressed()[0]:
                    cell = viewport.screen_to_cell(event.pos)
                    if cell is not None and TOOLS[selected_tool]["kind"] == "region":
                        if not paint_snapshot_pushed:
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            paint_snapshot_pushed = True
                        selected_ref, selected_cell, changed = handle_region_drag(level, selected_ref, cell)
                        dirty = changed or dirty
                    elif cell is not None and TOOLS[selected_tool]["kind"] == "tile":
                        if not paint_snapshot_pushed:
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            paint_snapshot_pushed = True
                        paint_tile(tiles, tile_variants, selected_tool, cell, current_tile_variant)
                        selected_ref = None
                        selected_cell = cell
                        dirty = True

        screen.fill((14, 16, 20))
        validation_issues = validate_level(level, tiles)
        mouse_pos = pygame.mouse.get_pos()
        hover_cell = None if mouse_pos[0] <= sidebar_width(ui_scale) or inspector_contains(screen, mouse_pos, ui_scale) else viewport.screen_to_cell(mouse_pos)
        draw_level(screen, viewport, sprites, tiles, tile_variants, level, selected_ref, selected_cell)
        if TOOLS[selected_tool]["kind"] == "region" or (selected_ref is not None and selected_ref[0] == "region"):
            draw_region_overlay(screen, viewport, level, selected_ref, font)
        draw_reference_overlay(screen, viewport, level, selected_ref, font)
        if show_logic:
            draw_logic_overlay(screen, viewport, level, font)
        if show_validation:
            draw_validation_overlay(screen, viewport, validation_issues)
        viewport.draw_grid(screen)
        draw_top_status(screen, font, selected_tool, selected_ref, hover_cell, dirty, validation_issues, viewport.cell_size, ui_scale)
        draw_sidebar(screen, font, selected_tool, str(level_path), dirty, patrol_mode, ui_scale)
        draw_inspector(screen, font, inspector_state, level, tiles, tile_variants, selected_ref, selected_cell, hover_cell, TOOLS[selected_tool]["label"], patrol_mode, sprites.variant_count, validation_issues, ui_scale)
        pygame.display.flip()
        clock.tick(60)

    pygame.quit()
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Edit TV roguelike JSON levels.")
    parser.add_argument("level_path", nargs="?", type=Path, help="Path to Assets/Levels/*.json.")
    parser.add_argument("--new", action="store_true", help="Create a new blank level instead of loading an existing file.")
    parser.add_argument("--scale-ui", type=float, default=1.0, help="Scale editor sidebar and text, e.g. --scale-ui 2.")
    parser.add_argument("--cell-size", type=int, default=32, help="Initial map cell size in screen pixels, clamped to 12..96.")
    return parser.parse_args()


def new_level(path: Path) -> dict:
    level = default_level()
    level["id"] = path.stem if path.name else "new_level"
    return level


def push_undo(
    undo_stack: list[dict],
    level: dict,
    tiles: list[list[str]],
    tile_variants: list[list[int]],
    selected_ref: tuple[str, int] | None,
    selected_cell: tuple[int, int] | None,
    current_tile_variant: int,
    patrol_mode: bool,
) -> None:
    undo_stack.append({
        "level": copy.deepcopy(level),
        "tiles": copy.deepcopy(tiles),
        "tile_variants": copy.deepcopy(tile_variants),
        "selected_ref": selected_ref,
        "selected_cell": selected_cell,
        "current_tile_variant": current_tile_variant,
        "patrol_mode": patrol_mode,
    })
    if len(undo_stack) > 100:
        del undo_stack[0]


def discard_unchanged_undo(undo_stack: list[dict], level: dict, tiles: list[list[str]], tile_variants: list[list[int]]) -> None:
    if undo_stack and undo_stack[-1]["level"] == level and undo_stack[-1]["tiles"] == tiles and undo_stack[-1]["tile_variants"] == tile_variants:
        undo_stack.pop()


def pop_undo(undo_stack: list[dict]) -> dict | None:
    if not undo_stack:
        return None
    return undo_stack.pop()


def handle_left_click(
    level: dict,
    tiles: list[list[str]],
    tile_variants: list[list[int]],
    selected_tool: int,
    cell: tuple[int, int],
    current_tile_variant: int,
    patrol_mode: bool,
    selected_ref: tuple[str, int] | None,
) -> tuple[tuple[str, int] | None, tuple[int, int] | None, bool]:
    if patrol_mode:
        ref = selected_ref or find_at(level, cell)
        if ref is not None and ref[0] == ENEMY_TYPE:
            enemy = level["enemies"][ref[1]]
            enemy.setdefault("patrol", [])
            enemy["patrol"].append([cell[0], cell[1]])
            return ref, None, True
        return selected_ref, None, False

    tool = TOOLS[selected_tool]
    existing = find_at(level, cell)
    if tool["kind"] == "cursor":
        return existing, None if existing is not None else cell, False

    if existing is not None:
        return existing, None, False

    if selected_ref is not None and selected_ref[0] in ("player", "exit"):
        move_selected(level, selected_ref, cell)
        return selected_ref, None, True

    if tool["kind"] == "tile":
        paint_tile(tiles, tile_variants, selected_tool, cell, current_tile_variant)
        return None, cell, True

    if selected_ref is not None and tool["kind"] != "tile":
        move_selected(level, selected_ref, cell)
        return selected_ref, None, True

    if tool["kind"] == "enemy":
        level["enemies"].append({"id": f"enemy_{cell[0]}_{cell[1]}", "type": "patrol", "branch": "none", "level": 3, "hp": 2, "x": cell[0], "y": cell[1], "patrol": [[cell[0], cell[1]]]})
        return (ENEMY_TYPE, len(level["enemies"]) - 1), None, True

    if tool["kind"] == "exit":
        exits = level.setdefault("exits", [])
        exits.append({
            "id": f"exit_{cell[0]}_{cell[1]}",
            "x": cell[0],
            "y": cell[1],
            "branch": "none",
            "requiresGate": "",
            "targetLevel": "",
        })
        return ("exit", len(exits) - 1), None, True

    obj = {"type": tool["value"], "x": cell[0], "y": cell[1], "variant": current_tile_variant}
    if tool["value"] == "gate":
        obj.update({"id": f"gate_{cell[0]}_{cell[1]}", "group": "start", "frame": "vertical", "requiresPlates": [], "requiresStories": [], "requiresEnemies": [], "requiresStats": []})
    if tool["value"] == "plate":
        obj["group"] = "start"
    if tool["value"] == "trap":
        obj["direction"] = {"x": 0.0, "y": -1.0}
    if tool["value"] == "story":
        obj.update({"id": f"story_{cell[0]}_{cell[1]}", "text": ""})
    if tool["value"] == "storyImage":
        obj.update({"id": f"story_image_{cell[0]}_{cell[1]}", "imagePath": ""})
    if tool["value"] == "checkpoint":
        obj.update({"id": f"checkpoint_{cell[0]}_{cell[1]}", "radius": 1.0})
    level["objects"].append(obj)
    return ("object", len(level["objects"]) - 1), None, True


def handle_region_click(level: dict, selected_ref: tuple[str, int] | None, cell: tuple[int, int]) -> tuple[tuple[str, int] | None, tuple[int, int] | None, bool]:
    if selected_ref is None or selected_ref[0] != "region":
        ref = region_at(level, cell)
        if ref is not None:
            return ref, cell, False
        region = create_region(level, cell)
        return ("region", len(level.setdefault("regions", [])) - 1), cell, True

    changed = toggle_region_cell(level, selected_ref[1], cell)
    return selected_ref, cell, changed


def handle_region_drag(level: dict, selected_ref: tuple[str, int] | None, cell: tuple[int, int]) -> tuple[tuple[str, int] | None, tuple[int, int] | None, bool]:
    if selected_ref is None or selected_ref[0] != "region":
        ref = region_at(level, cell)
        return (ref, cell, False) if ref is not None else (selected_ref, cell, False)

    changed = add_region_cell(level, selected_ref[1], cell)
    return selected_ref, cell, changed


def create_region(level: dict, cell: tuple[int, int]) -> dict:
    regions = level.setdefault("regions", [])
    region = {"id": f"region_{len(regions) + 1}", "runs": []}
    regions.append(region)
    add_region_cell(level, len(regions) - 1, cell)
    return region


def region_at(level: dict, cell: tuple[int, int]) -> tuple[str, int] | None:
    for index, region in enumerate(level.get("regions", []) or []):
        if cell in region_cells(region):
            return "region", index
    return None


def toggle_region_cell(level: dict, index: int, cell: tuple[int, int]) -> bool:
    regions = level.setdefault("regions", [])
    if not (0 <= index < len(regions)):
        return False
    cells = region_cells(regions[index])
    if cell in cells:
        cells.remove(cell)
    else:
        cells.add(cell)
    regions[index]["runs"] = cells_to_runs(cells)
    return True


def add_region_cell(level: dict, index: int, cell: tuple[int, int]) -> bool:
    regions = level.setdefault("regions", [])
    if not (0 <= index < len(regions)):
        return False
    cells = region_cells(regions[index])
    if cell in cells:
        return False
    cells.add(cell)
    regions[index]["runs"] = cells_to_runs(cells)
    return True


def region_cells(region: dict) -> set[tuple[int, int]]:
    cells: set[tuple[int, int]] = set()
    for run in region.get("runs", []) or []:
        y = int(run.get("y", 0))
        x0 = int(run.get("x", 0))
        length = max(0, int(run.get("length", 0)))
        for x in range(x0, x0 + length):
            cells.add((x, y))
    return cells


def cells_to_runs(cells: set[tuple[int, int]]) -> list[dict]:
    runs: list[dict] = []
    by_y: dict[int, list[int]] = {}
    for x, y in cells:
        by_y.setdefault(y, []).append(x)
    for y in sorted(by_y):
        xs = sorted(set(by_y[y]))
        if not xs:
            continue
        start = previous = xs[0]
        for x in xs[1:]:
            if x == previous + 1:
                previous = x
                continue
            runs.append({"x": start, "y": y, "length": previous - start + 1})
            start = previous = x
        runs.append({"x": start, "y": y, "length": previous - start + 1})
    return runs


def paint_tile(tiles: list[list[str]], tile_variants: list[list[int]], selected_tool: int, cell: tuple[int, int], variant: int) -> None:
    tool = TOOLS[selected_tool]
    if tool["kind"] == "tile":
        tiles[cell[0]][cell[1]] = tool["value"]
        tile_variants[cell[0]][cell[1]] = variant


def find_at(level: dict, cell: tuple[int, int]) -> tuple[str, int] | None:
    for index, obj in enumerate(level.get("objects", [])):
        if object_at(obj) == cell:
            return "object", index
    for index, enemy in enumerate(level.get("enemies", [])):
        if (int(enemy.get("x", 0)), int(enemy.get("y", 0))) == cell:
            return ENEMY_TYPE, index
    for index, exit_cell in enumerate(level.get("exits", [])):
        if (int(exit_cell.get("x", -1)), int(exit_cell.get("y", -1))) == cell:
            return "exit", index
    player = level.get("playerStart", {})
    if (int(player.get("x", -1)), int(player.get("y", -1))) == cell:
        return "player", 0
    return None


def find_at_screen(level: dict, viewport: Viewport, pos: tuple[int, int]) -> tuple[str, int] | None:
    for index in reversed(range(len(level.get("lights", []) or []))):
        if light_hit(viewport, level["lights"][index], pos):
            return "light", index
    for index in reversed(range(len(level.get("decorations", []) or []))):
        if decoration_hit(viewport, level["decorations"][index], pos):
            return "decoration", index
    return None


def decoration_hit(viewport: Viewport, decoration: dict, pos: tuple[int, int]) -> bool:
    rect = free_visual_rect(viewport, decoration)
    return rect.inflate(max(8, viewport.cell_size // 4), max(8, viewport.cell_size // 4)).collidepoint(pos)


def light_hit(viewport: Viewport, light: dict, pos: tuple[int, int]) -> bool:
    center = world_to_screen(viewport, safe_float(light.get("x", 0)), safe_float(light.get("y", 0)))
    return center.distance_to(pygame.Vector2(pos)) <= max(10, viewport.cell_size * 0.35)


def create_free_visual(level: dict, kind: str, viewport: Viewport, pos: tuple[int, int]) -> tuple[str, int]:
    x, y = screen_to_world(viewport, pos, snap=bool(pygame.key.get_mods() & pygame.KMOD_SHIFT))
    if kind == "light":
        lights = level.setdefault("lights", [])
        lights.append({
            "id": f"light_{len(lights) + 1}",
            "type": "point",
            "x": x,
            "y": y,
            "intensity": 0.7,
            "radius": 4.0,
            "color": "#d6f0ff",
            "rotation": 0,
            "outerAngle": 65,
            "innerAngle": 30,
        })
        return "light", len(lights) - 1

    decorations = level.setdefault("decorations", [])
    decorations.append({
        "id": f"texture_{len(decorations) + 1}",
        "texturePath": "Assets/Resources/Decor/texture.png",
        "x": x,
        "y": y,
        "scale": 1.0,
        "rotation": 0,
        "sortingOrder": 4,
        "castsShadow": False,
    })
    return "decoration", len(decorations) - 1


def is_free_ref(ref: tuple[str, int]) -> bool:
    return ref[0] in ("decoration", "light")


def move_free_selected(level: dict, ref: tuple[str, int], viewport: Viewport, pos: tuple[int, int], snap: bool) -> None:
    kind, index = ref
    target = None
    if kind == "decoration" and 0 <= index < len(level.get("decorations", [])):
        target = level["decorations"][index]
    elif kind == "light" and 0 <= index < len(level.get("lights", [])):
        target = level["lights"][index]
    if target is None:
        return
    x, y = screen_to_world(viewport, pos, snap)
    target["x"] = x
    target["y"] = y


def screen_to_world(viewport: Viewport, pos: tuple[int, int], snap: bool = False) -> tuple[float, float]:
    value = viewport.screen_to_cell_float(pos)
    x = value.x - 0.5
    y = value.y - 0.5
    if snap:
        x = round(x)
        y = round(y)
    return round(float(x), 3), round(float(y), 3)


def world_to_screen(viewport: Viewport, x: float, y: float) -> pygame.Vector2:
    return pygame.Vector2(
        viewport.offset.x + (x + 0.5) * viewport.cell_size,
        viewport.offset.y + (viewport.grid_height - 1 - y + 0.5) * viewport.cell_size,
    )


def can_drop_entity(level: dict, tiles: list[list[str]], ref: tuple[str, int], cell: tuple[int, int]) -> bool:
    x, y = cell
    if x < 0 or x >= len(tiles) or not tiles or y < 0 or y >= len(tiles[x]):
        return False
    if tiles[x][y] == "wall":
        return False

    existing = find_at(level, cell)
    return existing is None or existing == ref


def delete_selected(level: dict, ref: tuple[str, int]) -> None:
    kind, index = ref
    if kind == "object" and 0 <= index < len(level["objects"]):
        del level["objects"][index]
    elif kind == ENEMY_TYPE and 0 <= index < len(level["enemies"]):
        del level["enemies"][index]
    elif kind == "exit" and 0 <= index < len(level.get("exits", [])):
        del level["exits"][index]
    elif kind == "event" and 0 <= index < len(level.get("events", [])):
        del level["events"][index]
    elif kind == "region" and 0 <= index < len(level.get("regions", [])):
        del level["regions"][index]
    elif kind == "decoration" and 0 <= index < len(level.get("decorations", [])):
        del level["decorations"][index]
    elif kind == "light" and 0 <= index < len(level.get("lights", [])):
        del level["lights"][index]


def move_selected(level: dict, ref: tuple[str, int], cell: tuple[int, int]) -> None:
    kind, index = ref
    if kind == "object" and 0 <= index < len(level["objects"]):
        level["objects"][index]["x"] = cell[0]
        level["objects"][index]["y"] = cell[1]
    elif kind == ENEMY_TYPE and 0 <= index < len(level["enemies"]):
        level["enemies"][index]["x"] = cell[0]
        level["enemies"][index]["y"] = cell[1]
    elif kind == "player":
        player = level.setdefault("playerStart", {"x": 0, "y": 0})
        player["x"] = cell[0]
        player["y"] = cell[1]
    elif kind == "exit" and 0 <= index < len(level.get("exits", [])):
        exit_cell = level["exits"][index]
        exit_cell["x"] = cell[0]
        exit_cell["y"] = cell[1]


def rotate_selected(level: dict, ref: tuple[str, int] | None, degrees: float) -> bool:
    free_target = selected_free_visual(level, ref)
    if free_target is not None:
        rotation = safe_float(free_target.get("rotation", 0))
        free_target["rotation"] = round((rotation + degrees) % 360, 3)
        return True

    obj = selected_object(level, ref)
    if obj is None or "direction" not in obj:
        return False

    direction = obj.get("direction") or {"x": 0.0, "y": -1.0}
    x = float(direction.get("x", 0.0))
    y = float(direction.get("y", -1.0))
    length = math.hypot(x, y)
    if length <= 0.0001:
        x, y = 0.0, -1.0
    else:
        x /= length
        y /= length

    radians = math.radians(degrees)
    cos_a = math.cos(radians)
    sin_a = math.sin(radians)
    obj["direction"] = {
        "x": round(x * cos_a - y * sin_a, 4),
        "y": round(x * sin_a + y * cos_a, 4),
    }
    return True


def toggle_selected_frame(level: dict, ref: tuple[str, int] | None) -> bool:
    obj = selected_object(level, ref)
    if obj is None or obj.get("type") != "gate":
        return False

    obj["frame"] = "horizontal" if obj.get("frame") != "horizontal" else "vertical"
    return True


def selected_object(level: dict, ref: tuple[str, int] | None) -> dict | None:
    if ref is None:
        return None

    kind, index = ref
    if kind == "object" and 0 <= index < len(level["objects"]):
        return level["objects"][index]
    return None


def selected_free_visual(level: dict, ref: tuple[str, int] | None) -> dict | None:
    if ref is None:
        return None

    kind, index = ref
    if kind == "decoration" and 0 <= index < len(level.get("decorations", [])):
        return level["decorations"][index]
    if kind == "light" and 0 <= index < len(level.get("lights", [])):
        return level["lights"][index]
    return None


def selected_directional_object(level: dict, ref: tuple[str, int] | None) -> dict | None:
    obj = selected_object(level, ref)
    if obj is not None and "direction" in obj:
        return obj
    return None


def directional_handle_hit(level: dict, viewport: Viewport, ref: tuple[str, int] | None, pos: tuple[int, int]) -> bool:
    obj = selected_directional_object(level, ref)
    if obj is None:
        return False

    center, handle = direction_points(viewport, obj)
    radius = max(10, viewport.cell_size * 0.22)
    return handle.distance_to(pygame.Vector2(pos)) <= radius or center.distance_to(pygame.Vector2(pos)) <= viewport.cell_size * 0.48


def update_direction_from_mouse(level: dict, viewport: Viewport, ref: tuple[str, int] | None, pos: tuple[int, int]) -> bool:
    obj = selected_directional_object(level, ref)
    if obj is None:
        return False

    center = pygame.Vector2(viewport.cell_to_screen(int(obj.get("x", 0)), int(obj.get("y", 0))).center)
    delta = pygame.Vector2(pos) - center
    if delta.length_squared() <= 0.0001:
        return False

    delta = delta.normalize()
    obj["direction"] = {
        "x": round(delta.x, 4),
        "y": round(-delta.y, 4),
    }
    return True


def direction_points(viewport: Viewport, obj: dict) -> tuple[pygame.Vector2, pygame.Vector2]:
    direction = obj.get("direction") or {"x": 0.0, "y": -1.0}
    x = float(direction.get("x", 0.0))
    y = float(direction.get("y", -1.0))
    length = math.hypot(x, y)
    if length <= 0.0001:
        x, y = 0.0, -1.0
    else:
        x /= length
        y /= length

    rect = viewport.cell_to_screen(int(obj.get("x", 0)), int(obj.get("y", 0)))
    center = pygame.Vector2(rect.center)
    handle = center + pygame.Vector2(x, -y) * (viewport.cell_size * 0.48)
    return center, handle


def draw_level(
    screen: pygame.Surface,
    viewport: Viewport,
    sprites: SpriteBank,
    tiles: list[list[str]],
    tile_variants: list[list[int]],
    level: dict,
    selected_ref: tuple[str, int] | None,
    selected_cell: tuple[int, int] | None,
) -> None:
    cell_size = viewport.cell_size
    width = len(tiles)
    height = len(tiles[0]) if width else 0
    for x in range(width):
        for y in range(height):
            rect = viewport.cell_to_screen(x, y)
            screen.blit(sprites.get(tiles[x][y], cell_size, tile_variants[x][y]), rect)
            if selected_cell == (x, y):
                pygame.draw.rect(screen, (255, 238, 120), rect, 3)

    draw_decorations(screen, viewport, sprites, level, selected_ref)
    draw_lights(screen, viewport, level, selected_ref, font=None)

    for index, exit_cell in enumerate(level.get("exits", [])):
        draw_marker(screen, viewport, sprites, "exit", exit_cell, cell_size, selected_ref == ("exit", index))
    draw_marker(screen, viewport, sprites, "player", level.get("playerStart", {}), cell_size, selected_ref == ("player", 0))

    for index, obj in enumerate(level.get("objects", [])):
        draw_marker(screen, viewport, sprites, obj.get("type", "story"), obj, cell_size, selected_ref == ("object", index))
        draw_direction(screen, viewport, obj, selected_ref == ("object", index))

    for index, enemy in enumerate(level.get("enemies", [])):
        draw_marker(screen, viewport, sprites, "enemy", enemy, cell_size, selected_ref == (ENEMY_TYPE, index))
        draw_patrol(screen, viewport, enemy)


def draw_marker(screen: pygame.Surface, viewport: Viewport, sprites: SpriteBank, name: str, data: dict, cell_size: int, selected: bool = False) -> None:
    x = int(data.get("x", 0))
    y = int(data.get("y", 0))
    rect = viewport.cell_to_screen(x, y)
    if name == "checkpoint":
        overlay = pygame.Surface((cell_size, cell_size), pygame.SRCALPHA)
        pad = max(4, cell_size // 6)
        points = [
            (cell_size // 2, pad),
            (cell_size - pad, cell_size // 2),
            (cell_size // 2, cell_size - pad),
            (pad, cell_size // 2),
        ]
        pygame.draw.polygon(overlay, (245, 250, 255, 92), points)
        pygame.draw.polygon(overlay, (245, 250, 255, 178), points, max(1, cell_size // 16))
        screen.blit(overlay, rect)
        if selected:
            pygame.draw.rect(screen, (255, 238, 120), rect, 3)
        return

    screen.blit(sprites.get(name, cell_size, int(data.get("variant", -1))), rect)
    if selected:
        pygame.draw.rect(screen, (255, 238, 120), rect, 3)


def draw_decorations(screen: pygame.Surface, viewport: Viewport, sprites: SpriteBank, level: dict, selected_ref: tuple[str, int] | None) -> None:
    indexed = list(enumerate(level.get("decorations", []) or []))
    indexed.sort(key=lambda item: safe_int(item[1].get("sortingOrder", 4), 4))
    for index, decoration in indexed:
        rect = free_visual_rect(viewport, decoration)
        surface = sprites.get_path(str(decoration.get("texturePath", "")), rect.width, rect.height)
        rotation = safe_float(decoration.get("rotation", 0))
        if abs(rotation) > 0.001:
            surface = pygame.transform.rotate(surface, -rotation)
            rect = surface.get_rect(center=rect.center)
        screen.blit(surface, rect)
        if selected_ref == ("decoration", index):
            pygame.draw.rect(screen, (255, 238, 120), rect, max(2, viewport.cell_size // 14))
            handle = rotation_handle(viewport, decoration)
            pygame.draw.line(screen, (255, 238, 120), rect.center, handle, max(1, viewport.cell_size // 18))
            pygame.draw.circle(screen, (255, 238, 120), handle, max(4, viewport.cell_size // 10), 2)
        elif bool(decoration.get("castsShadow", False)):
            pygame.draw.rect(screen, (90, 120, 130), rect, max(1, viewport.cell_size // 24))


def draw_lights(screen: pygame.Surface, viewport: Viewport, level: dict, selected_ref: tuple[str, int] | None, font: pygame.font.Font | None) -> None:
    overlay = pygame.Surface(screen.get_size(), pygame.SRCALPHA)
    for index, light in enumerate(level.get("lights", []) or []):
        center = world_to_screen(viewport, safe_float(light.get("x", 0)), safe_float(light.get("y", 0)))
        center_pos = (round(center.x), round(center.y))
        radius = max(2, int(safe_float(light.get("radius", 4.0), 4.0) * viewport.cell_size))
        selected = selected_ref == ("light", index)
        color = parse_preview_color(str(light.get("color", "#d6f0ff")), 54 if selected else 34)
        outline = parse_preview_color(str(light.get("color", "#d6f0ff")), 210 if selected else 135)
        if light.get("type", "point") == "cone":
            points = cone_points(center, radius, safe_float(light.get("rotation", 0)), safe_float(light.get("outerAngle", 65), 65))
            pygame.draw.polygon(overlay, color, points)
            pygame.draw.lines(overlay, outline, True, points, max(2, viewport.cell_size // 18))
        else:
            pygame.draw.circle(overlay, color, center_pos, radius)
            pygame.draw.circle(overlay, outline, center_pos, radius, max(2, viewport.cell_size // 18))
        pygame.draw.circle(overlay, outline, center_pos, max(4, viewport.cell_size // 10))
    screen.blit(overlay, (0, 0))


def free_visual_rect(viewport: Viewport, decoration: dict) -> pygame.Rect:
    scale = max(0.05, safe_float(decoration.get("scale", 1.0), 1.0))
    size = max(1, round(viewport.cell_size * scale))
    center = world_to_screen(viewport, safe_float(decoration.get("x", 0)), safe_float(decoration.get("y", 0)))
    return pygame.Rect(round(center.x - size * 0.5), round(center.y - size * 0.5), size, size)


def rotation_handle(viewport: Viewport, decoration: dict) -> tuple[int, int]:
    center = world_to_screen(viewport, safe_float(decoration.get("x", 0)), safe_float(decoration.get("y", 0)))
    rotation = math.radians(safe_float(decoration.get("rotation", 0)))
    distance = viewport.cell_size * max(0.7, safe_float(decoration.get("scale", 1.0), 1.0) * 0.65)
    return round(center.x + math.cos(rotation) * distance), round(center.y - math.sin(rotation) * distance)


def cone_points(center: pygame.Vector2, radius: int, rotation: float, outer_angle: float) -> list[tuple[int, int]]:
    half = math.radians(max(1.0, min(360.0, outer_angle)) * 0.5)
    direction = math.radians(rotation)
    steps = 14
    points = [(round(center.x), round(center.y))]
    for step in range(steps + 1):
        angle = direction - half + (half * 2 * step / steps)
        points.append((round(center.x + math.cos(angle) * radius), round(center.y - math.sin(angle) * radius)))
    return points


def parse_preview_color(value: str, alpha: int) -> tuple[int, int, int, int]:
    text = value.strip()
    if text.startswith("#") and len(text) == 7:
        try:
            return int(text[1:3], 16), int(text[3:5], 16), int(text[5:7], 16), alpha
        except ValueError:
            pass
    return 214, 240, 255, alpha


def safe_float(value: object, fallback: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def safe_int(value: object, fallback: int = 0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def draw_direction(screen: pygame.Surface, viewport: Viewport, obj: dict, selected: bool = False) -> None:
    direction = obj.get("direction")
    if not direction:
        return

    start, end = direction_points(viewport, obj)
    color = (128, 230, 255)
    pygame.draw.line(screen, color, start, end, max(2, viewport.cell_size // 14))
    pygame.draw.circle(screen, color, end, max(3, viewport.cell_size // 10))
    if selected:
        pygame.draw.circle(screen, (255, 238, 120), end, max(7, viewport.cell_size // 5), 2)
        pygame.draw.circle(screen, (255, 238, 120), start, max(12, viewport.cell_size // 2), 1)


def draw_patrol(screen: pygame.Surface, viewport: Viewport, enemy: dict) -> None:
    points = enemy.get("patrol", [])
    if len(points) < 2:
        return
    screen_points = []
    for point in points:
        if len(point) < 2:
            continue
        rect = viewport.cell_to_screen(int(point[0]), int(point[1]))
        screen_points.append(rect.center)
    if len(screen_points) >= 2:
        pygame.draw.lines(screen, (255, 182, 80), False, screen_points, 2)


def draw_validation_overlay(screen: pygame.Surface, viewport: Viewport, issues: list) -> None:
    for issue in issues:
        if issue.cell is None:
            continue
        x, y = issue.cell
        if not (0 <= x < viewport.grid_width and 0 <= y < viewport.grid_height):
            continue
        rect = viewport.cell_to_screen(x, y)
        color = (255, 72, 82) if issue.severity == "error" else (255, 198, 74)
        pygame.draw.rect(screen, color, rect, max(2, viewport.cell_size // 12))


def draw_region_overlay(screen: pygame.Surface, viewport: Viewport, level: dict, selected_ref: tuple[str, int] | None, font: pygame.font.Font) -> None:
    overlay = pygame.Surface(screen.get_size(), pygame.SRCALPHA)
    selected_index = selected_ref[1] if selected_ref is not None and selected_ref[0] == "region" else -1
    for index, region in enumerate(level.get("regions", []) or []):
        selected = index == selected_index
        fill = (64, 190, 255, 82) if selected else (80, 156, 255, 36)
        outline = (120, 230, 255, 230) if selected else (80, 156, 255, 130)
        for cell in region_cells(region):
            rect = viewport.cell_to_screen(cell[0], cell[1])
            pygame.draw.rect(overlay, fill, rect)
            pygame.draw.rect(overlay, outline, rect, max(1, viewport.cell_size // 18))
        cells = region_cells(region)
        if cells:
            min_x = min(x for x, _y in cells)
            max_y = max(y for _x, y in cells)
            label_pos = viewport.cell_to_screen(min_x, max_y)
            text = region.get("id", f"region_{index + 1}")
            overlay.blit(font.render(str(text)[:22], True, (226, 248, 255)), (label_pos.x, max(0, label_pos.y - font.get_height())))
    screen.blit(overlay, (0, 0))


def draw_reference_overlay(screen: pygame.Surface, viewport: Viewport, level: dict, selected_ref: tuple[str, int] | None, font: pygame.font.Font) -> None:
    if selected_ref is None:
        return

    overlay = pygame.Surface(screen.get_size(), pygame.SRCALPHA)
    kind, index = selected_ref
    if kind == "object" and 0 <= index < len(level.get("objects", [])):
        obj = level["objects"][index]
        if obj.get("type") == "gate":
            draw_gate_references(overlay, viewport, level, obj, font)
    elif kind == "event" and 0 <= index < len(level.get("events", [])):
        draw_event_references(overlay, viewport, level, level["events"][index], font)

    screen.blit(overlay, (0, 0))


def draw_gate_references(surface: pygame.Surface, viewport: Viewport, level: dict, gate: dict, font: pygame.font.Font) -> None:
    gate_cell = object_at(gate)
    gate_center = viewport.cell_to_screen(*gate_cell).center
    plate_groups = set(str(value) for value in gate.get("requiresPlates", []) or [] if str(value))
    story_ids = set(str(value) for value in gate.get("requiresStories", []) or [] if str(value))
    enemy_ids = set(str(value) for value in gate.get("requiresEnemies", []) or [] if str(value))
    gate_id = str(gate.get("id", ""))

    for obj in level.get("objects", []) or []:
        obj_type = obj.get("type")
        cell = object_at(obj)
        if obj_type == "plate" and str(obj.get("group", "")) in plate_groups:
            draw_linked_cell(surface, viewport, gate_center, cell, (116, 226, 255, 80), (116, 226, 255, 220), "plate", font)
        elif obj_type in ("story", "storyImage") and str(obj.get("id", "")) in story_ids:
            draw_linked_cell(surface, viewport, gate_center, cell, (176, 146, 255, 78), (196, 176, 255, 230), "story", font)

    for enemy in level.get("enemies", []) or []:
        if str(enemy.get("id", "")) in enemy_ids:
            cell = (int(enemy.get("x", 0)), int(enemy.get("y", 0)))
            draw_linked_cell(surface, viewport, gate_center, cell, (255, 106, 106, 74), (255, 130, 130, 230), "enemy", font)

    for exit_cell in level.get("exits", []) or []:
        if gate_id and str(exit_cell.get("requiresGate", "")) == gate_id:
            cell = (int(exit_cell.get("x", 0)), int(exit_cell.get("y", 0)))
            draw_linked_cell(surface, viewport, gate_center, cell, (134, 255, 154, 72), (148, 255, 172, 230), "exit", font)


def draw_event_references(surface: pygame.Surface, viewport: Viewport, level: dict, event: dict, font: pygame.font.Font) -> None:
    trigger = event.get("trigger", "")
    if trigger == "enterRegion":
        region = find_region(level, str(event.get("region", "")))
        if region is not None:
            for cell in region_cells(region):
                draw_reference_cell(surface, viewport, cell, (64, 190, 255, 64), (120, 230, 255, 210))
    elif trigger == "enemyKilled":
        enemy = find_enemy_by_id(level, str(event.get("enemyId", "")))
        if enemy is not None:
            draw_reference_cell(surface, viewport, (int(enemy.get("x", 0)), int(enemy.get("y", 0))), (255, 106, 106, 74), (255, 130, 130, 230))
    elif trigger == "enemyGroupCleared":
        group = str(event.get("enemyGroup", ""))
        for enemy in level.get("enemies", []) or []:
            if group and str(enemy.get("group", "")) == group:
                draw_reference_cell(surface, viewport, (int(enemy.get("x", 0)), int(enemy.get("y", 0))), (255, 106, 106, 74), (255, 130, 130, 230))

    for action in event.get("actions", []) or []:
        if not isinstance(action, dict) or "x" not in action or "y" not in action:
            continue
        cell = (int(action.get("x", 0)), int(action.get("y", 0)))
        draw_reference_cell(surface, viewport, cell, (255, 214, 104, 66), (255, 222, 126, 230))
        rect = viewport.cell_to_screen(*cell)
        label = str(action.get("type", "action"))[:16]
        surface.blit(font.render(label, True, (255, 238, 180)), (rect.x, rect.y - font.get_height()))


def draw_linked_cell(
    surface: pygame.Surface,
    viewport: Viewport,
    source_center: tuple[int, int],
    cell: tuple[int, int],
    fill: tuple[int, int, int, int],
    outline: tuple[int, int, int, int],
    label: str,
    font: pygame.font.Font,
) -> None:
    rect = viewport.cell_to_screen(*cell)
    pygame.draw.line(surface, outline, source_center, rect.center, max(1, viewport.cell_size // 16))
    draw_reference_cell(surface, viewport, cell, fill, outline)
    surface.blit(font.render(label, True, outline[:3]), (rect.x, rect.y - font.get_height()))


def draw_reference_cell(surface: pygame.Surface, viewport: Viewport, cell: tuple[int, int], fill: tuple[int, int, int, int], outline: tuple[int, int, int, int]) -> None:
    rect = viewport.cell_to_screen(*cell)
    pygame.draw.rect(surface, fill, rect)
    pygame.draw.rect(surface, outline, rect, max(2, viewport.cell_size // 12))


def find_region(level: dict, region_id: str) -> dict | None:
    for region in level.get("regions", []) or []:
        if region_id and str(region.get("id", "")) == region_id:
            return region
    return None


def find_enemy_by_id(level: dict, enemy_id: str) -> dict | None:
    for enemy in level.get("enemies", []) or []:
        if enemy_id and str(enemy.get("id", "")) == enemy_id:
            return enemy
    return None


def draw_logic_overlay(screen: pygame.Surface, viewport: Viewport, level: dict, font: pygame.font.Font) -> None:
    logic = level.get("logic", {})
    overlay = pygame.Surface(screen.get_size(), pygame.SRCALPHA)
    for item in logic.get("branchTriggers", []) or []:
        area = item.get("area")
        if area:
            draw_area(overlay, viewport, area, (80, 156, 255, 46), (80, 156, 255, 150))
    for item in logic.get("branchBlocks", []) or []:
        area = item.get("area")
        if area:
            draw_area(overlay, viewport, area, (255, 72, 82, 42), (255, 72, 82, 170))
    screen.blit(overlay, (0, 0))

    for obj in level.get("objects", []):
        if obj.get("type") != "gate":
            continue
        labels = [f"P:{value}" for value in obj.get("requiresPlates", []) or []]
        labels.extend(f"S:{value}" for value in obj.get("requiresStories", []) or [])
        labels.extend(f"E:{value}" for value in obj.get("requiresEnemies", []) or [])
        labels.extend(f"V:{format_stat_condition(value)}" for value in obj.get("requiresStats", []) or [])
        if not labels:
            continue
        rect = viewport.cell_to_screen(int(obj.get("x", 0)), int(obj.get("y", 0)))
        text = " ".join(labels)
        screen.blit(font.render(text, True, (230, 244, 255)), (rect.x, rect.y - font.get_height()))


def draw_area(surface: pygame.Surface, viewport: Viewport, area: dict, fill: tuple[int, int, int, int], outline: tuple[int, int, int, int]) -> None:
    x1 = int(area.get("x1", 0))
    y1 = int(area.get("y1", 0))
    x2 = int(area.get("x2", x1))
    y2 = int(area.get("y2", y1))
    min_x, max_x = min(x1, x2), max(x1, x2)
    min_y, max_y = min(y1, y2), max(y1, y2)
    top_left = viewport.cell_to_screen(min_x, max_y)
    bottom_right = viewport.cell_to_screen(max_x, min_y)
    rect = pygame.Rect(top_left.x, top_left.y, bottom_right.right - top_left.x, bottom_right.bottom - top_left.y)
    pygame.draw.rect(surface, fill, rect)
    pygame.draw.rect(surface, outline, rect, max(2, viewport.cell_size // 14))


def format_stat_condition(value: object) -> str:
    if isinstance(value, list) and len(value) >= 3:
        return f"{value[1]} {value[0]} {value[2]}"
    return str(value)


if __name__ == "__main__":
    raise SystemExit(main())
