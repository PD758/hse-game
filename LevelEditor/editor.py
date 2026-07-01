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
from ui import draw_sidebar, save_button_at, sidebar_width, tool_at
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
    screen = pygame.display.set_mode((1280, 820), pygame.RESIZABLE)
    pygame.display.set_caption(f"Level Editor - {level_path.name}")
    font = pygame.font.SysFont("monospace", round(15 * ui_scale))
    clock = pygame.time.Clock()

    sprites = SpriteBank(repo_root())
    viewport = Viewport(width, height, args.cell_size)
    viewport.offset.x = 20 + sidebar_width(ui_scale)
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
                viewport.zoom(event.y, pygame.mouse.get_pos())
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
                        changed, current_tile_variant = handle_inspector_click(inspector_state, level, selected_ref, selected_cell, tile_variants, current_tile_variant, event.pos)
                        if inspector_state.pending_select_ref is not None:
                            selected_ref = inspector_state.pending_select_ref
                            selected_cell = None
                            inspector_state.pending_select_ref = None
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
                        patrol_mode = False
                        continue
                    cell = viewport.screen_to_cell(event.pos)
                    if cell is not None:
                        inspector_state.clear_text()
                        if TOOLS[selected_tool]["kind"] == "cursor" and not patrol_mode:
                            existing = find_at(level, cell)
                            if existing is not None:
                                push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                                selected_ref = existing
                                selected_cell = None
                                drag_ref = existing
                                drag_last_cell = cell
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
            elif event.type == pygame.MOUSEMOTION:
                if panning or (pygame.mouse.get_pressed()[0] and pygame.key.get_pressed()[pygame.K_SPACE]):
                    viewport.pan(event.rel)
                elif angle_drag:
                    dirty = update_direction_from_mouse(level, viewport, selected_ref, event.pos) or dirty
                elif drag_ref is not None:
                    cell = viewport.screen_to_cell(event.pos)
                    if cell is not None and cell != drag_last_cell and can_drop_entity(level, tiles, drag_ref, cell):
                        move_selected(level, drag_ref, cell)
                        selected_ref = drag_ref
                        selected_cell = None
                        drag_last_cell = cell
                        dirty = True
                elif pygame.mouse.get_pressed()[0]:
                    cell = viewport.screen_to_cell(event.pos)
                    if cell is not None and TOOLS[selected_tool]["kind"] == "tile":
                        if not paint_snapshot_pushed:
                            push_undo(undo_stack, level, tiles, tile_variants, selected_ref, selected_cell, current_tile_variant, patrol_mode)
                            paint_snapshot_pushed = True
                        paint_tile(tiles, tile_variants, selected_tool, cell, current_tile_variant)
                        selected_ref = None
                        selected_cell = cell
                        dirty = True

        screen.fill((14, 16, 20))
        validation_issues = validate_level(level, tiles)
        draw_level(screen, viewport, sprites, tiles, tile_variants, level, selected_ref, selected_cell)
        if show_logic:
            draw_logic_overlay(screen, viewport, level, font)
        if show_validation:
            draw_validation_overlay(screen, viewport, validation_issues)
        viewport.draw_grid(screen)
        draw_sidebar(screen, font, selected_tool, str(level_path), dirty, patrol_mode, ui_scale)
        mouse_pos = pygame.mouse.get_pos()
        hover_cell = None if mouse_pos[0] <= sidebar_width(ui_scale) or inspector_contains(screen, mouse_pos, ui_scale) else viewport.screen_to_cell(mouse_pos)
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
        level["enemies"].append({"id": f"enemy_{cell[0]}_{cell[1]}", "type": "announcer", "branch": "none", "level": 3, "hp": 2, "x": cell[0], "y": cell[1], "patrol": [[cell[0], cell[1]]]})
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
    level["objects"].append(obj)
    return ("object", len(level["objects"]) - 1), None, True


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
    screen.blit(sprites.get(name, cell_size, int(data.get("variant", -1))), rect)
    if selected:
        pygame.draw.rect(screen, (255, 238, 120), rect, 3)


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
