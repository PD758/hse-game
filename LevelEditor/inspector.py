from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Callable

import pygame


BRANCHES = ("none", "puzzle", "combat")
FRAMES = ("vertical", "horizontal")


@dataclass
class InspectorAction:
    rect: pygame.Rect
    kind: str
    field: str = ""
    value: object = ""
    amount: float = 0.0
    target: tuple[str, int] | None = None


class InspectorState:
    def __init__(self) -> None:
        self.active_ref: tuple[str, int] | None = None
        self.active_field = ""
        self.text = ""
        self.actions: list[InspectorAction] = []
        self.pending_select_ref: tuple[str, int] | None = None

    def begin_text(self, ref: tuple[str, int], field: str, value: object) -> None:
        self.active_ref = ref
        self.active_field = field
        self.text = "" if value is None else str(value)

    def clear_text(self) -> None:
        self.active_ref = None
        self.active_field = ""
        self.text = ""


def inspector_width(scale: float = 1.0) -> int:
    return round(320 * scale)


def draw_inspector(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    level: dict,
    tiles: list[list[str]],
    tile_variants: list[list[int]],
    selected_ref: tuple[str, int] | None,
    selected_cell: tuple[int, int] | None,
    hover_cell: tuple[int, int] | None,
    selected_tool_label: str,
    patrol_mode: bool,
    variant_count_for: Callable[[str], int],
    validation_issues: list[Any] | None = None,
    scale: float = 1.0,
) -> None:
    state.actions = []
    width = inspector_width(scale)
    x = screen.get_width() - width
    margin = round(14 * scale)
    y = round(14 * scale)
    panel = pygame.Rect(x, 0, width, screen.get_height())
    pygame.draw.rect(screen, (22, 24, 28), panel)

    y = draw_text(screen, font, "Inspector", x + margin, y, (230, 234, 238))
    y += round(10 * scale)

    if selected_ref is None:
        if selected_cell is not None and inside_tiles(tiles, selected_cell):
            tx, ty = selected_cell
            y = draw_text(screen, font, f"Tile: {tiles[tx][ty]}", x + margin, y, (166, 212, 230))
            y = draw_text(screen, font, f"x: {tx}   y: {ty}", x + margin, y, (188, 196, 204))
            y = draw_variant_buttons(screen, font, state, tile_variants[tx][ty], variant_count_for(tiles[tx][ty]), x + margin, y + round(8 * scale), width - margin * 2, scale, "set_tile_variant")
            y += round(6 * scale)
        y = draw_text(screen, font, f"Tool: {selected_tool_label}", x + margin, y, (188, 196, 204))
        if hover_cell is not None:
            y = draw_text(screen, font, f"Cell: {hover_cell[0]}, {hover_cell[1]}", x + margin, y, (166, 212, 230))
        draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(14 * scale), width - margin * 2, scale)
        return

    target = target_for_ref(level, selected_ref)
    if target is None:
        draw_text(screen, font, "Nothing selected", x + margin, y, (188, 196, 204))
        return

    kind, _index = selected_ref
    title = target_title(kind, target)
    y = draw_text(screen, font, title, x + margin, y, (166, 212, 230))
    y = draw_text(screen, font, f"x: {target.get('x', 0)}   y: {target.get('y', 0)}", x + margin, y, (188, 196, 204))
    y += round(8 * scale)

    if kind == "object":
        obj_type = target.get("type", "")
        y = draw_variant_buttons(screen, font, state, int(target.get("variant", -1)), variant_count_for(obj_type), x + margin, y, width - margin * 2, scale, "set_int", field="variant")
        if obj_type == "gate":
            y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
            y = draw_text_field(screen, font, state, selected_ref, "group", target.get("group", ""), x + margin, y, width - margin * 2, scale)
            y = draw_text_field(screen, font, state, selected_ref, "requiresPlates", target.get("requiresPlates", []), x + margin, y, width - margin * 2, scale)
            y = draw_text_field(screen, font, state, selected_ref, "requiresStories", target.get("requiresStories", []), x + margin, y, width - margin * 2, scale)
            y = draw_enum_buttons(screen, font, state, "frame", target.get("frame", "vertical"), FRAMES, x + margin, y, width - margin * 2, scale)
        elif obj_type == "plate":
            y = draw_text_field(screen, font, state, selected_ref, "group", target.get("group", ""), x + margin, y, width - margin * 2, scale)
        elif obj_type == "trap":
            angle = direction_angle(target)
            y = draw_text(screen, font, f"angle: {round(angle)} deg", x + margin, y, (188, 196, 204))
            y = draw_text(screen, font, "Drag arrow handle on map", x + margin, y, (166, 212, 230))
            y = draw_text(screen, font, "Alt+drag cell also rotates", x + margin, y, (132, 140, 148))
        elif obj_type == "story":
            y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
            y = draw_text_field(screen, font, state, selected_ref, "text", target.get("text", ""), x + margin, y, width - margin * 2, scale, multiline=True)
        else:
            y = draw_text(screen, font, "No editable properties", x + margin, y, (132, 140, 148))
    elif kind == "enemy":
        y = draw_enum_buttons(screen, font, state, "branch", target.get("branch", "none"), BRANCHES, x + margin, y, width - margin * 2, scale)
        patrol = target.setdefault("patrol", [])
        y = draw_text(screen, font, f"patrol points: {len(patrol)}", x + margin, y, (188, 196, 204))
        y = draw_button_row(screen, font, state, [("remove_patrol", "Remove last"), ("clear_patrol", "Clear")], x + margin, y, width - margin * 2, scale)
        if patrol_mode:
            y = draw_text(screen, font, "Patrol edit: click map to add", x + margin, y, (166, 212, 230))
    elif kind == "exit":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "targetLevel", target.get("targetLevel", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "requiresGate", target.get("requiresGate", ""), x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "branch", target.get("branch", "none"), BRANCHES, x + margin, y, width - margin * 2, scale)
        y = draw_text(screen, font, "Click an empty cell to move.", x + margin, y, (188, 196, 204))
    elif kind == "player":
        y = draw_text(screen, font, "Click an empty cell to move.", x + margin, y, (188, 196, 204))

    draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(16 * scale), width - margin * 2, scale)


def handle_key(state: InspectorState, level: dict, selected_ref: tuple[str, int] | None, event: pygame.event.Event) -> bool:
    if not state.active_field or state.active_ref is None or state.active_ref != selected_ref:
        state.clear_text()
        return False

    if event.key in (pygame.K_RETURN, pygame.K_KP_ENTER) and state.active_field == "text" and not pygame.key.get_mods() & pygame.KMOD_CTRL:
        state.text += "\n"
    elif event.key in (pygame.K_RETURN, pygame.K_KP_ENTER, pygame.K_ESCAPE):
        state.clear_text()
        return False
    elif event.key == pygame.K_BACKSPACE:
        state.text = state.text[:-1]
    elif event.unicode and is_allowed_text(event.unicode, state.active_field):
        state.text += event.unicode
    else:
        return False

    target = target_for_ref(level, state.active_ref)
    if target is not None:
        target[state.active_field] = coerce_text_value(state.active_field, state.text)
        return True
    return False


def handle_click(
    state: InspectorState,
    level: dict,
    selected_ref: tuple[str, int] | None,
    selected_cell: tuple[int, int] | None,
    tile_variants: list[list[int]],
    current_tile_variant: int,
    pos: tuple[int, int],
) -> tuple[bool, int]:
    for action in state.actions:
        if not action.rect.collidepoint(pos):
            continue

        if action.kind == "issue":
            state.pending_select_ref = action.target
            return False, current_tile_variant

        if action.kind == "set_tile_variant":
            if selected_cell is None or not inside_tiles(tile_variants, selected_cell):
                return False, current_tile_variant
            tx, ty = selected_cell
            value = int(action.value)
            tile_variants[tx][ty] = value
            state.clear_text()
            return True, value

        target = target_for_ref(level, selected_ref)
        if target is None:
            return False, current_tile_variant

        if action.kind == "text":
            state.begin_text(selected_ref, action.field, target.get(action.field, ""))
            return False, current_tile_variant
        if action.kind == "set":
            target[action.field] = action.value
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "set_int":
            target[action.field] = int(action.value)
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "rotate":
            rotate_direction(target, action.amount)
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "remove_patrol":
            patrol = target.setdefault("patrol", [])
            if patrol:
                patrol.pop()
                return True, current_tile_variant
            return False, current_tile_variant
        if action.kind == "clear_patrol":
            if target.get("patrol"):
                target["patrol"] = []
                return True, current_tile_variant
            return False, current_tile_variant

    state.clear_text()
    return False, current_tile_variant


def point_inside(screen: pygame.Surface, pos: tuple[int, int], scale: float = 1.0) -> bool:
    return pos[0] >= screen.get_width() - inspector_width(scale)


def target_for_ref(level: dict, ref: tuple[str, int] | None) -> dict | None:
    if ref is None:
        return None

    kind, index = ref
    if kind == "object" and 0 <= index < len(level.get("objects", [])):
        return level["objects"][index]
    if kind == "enemy" and 0 <= index < len(level.get("enemies", [])):
        return level["enemies"][index]
    if kind == "player":
        return level.setdefault("playerStart", {"x": 0, "y": 0})
    if kind == "exit" and 0 <= index < len(level.get("exits", [])):
        return level["exits"][index]
    return None


def draw_text(screen: pygame.Surface, font: pygame.font.Font, text: str, x: int, y: int, color: tuple[int, int, int]) -> int:
    screen.blit(font.render(text, True, color), (x, y))
    return y + font.get_height() + 4


def draw_text_field(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    ref: tuple[str, int],
    field: str,
    value: object,
    x: int,
    y: int,
    width: int,
    scale: float,
    multiline: bool = False,
) -> int:
    label = f"{field}:"
    y = draw_text(screen, font, label, x, y, (188, 196, 204))
    height = round((76 if multiline else 28) * scale)
    rect = pygame.Rect(x, y, width, height)
    active = state.active_ref == ref and state.active_field == field
    color = (58, 66, 74) if active else (36, 40, 46)
    pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
    shown = state.text if active else display_text_value(value)
    draw_field_value(screen, font, shown, rect, scale)
    state.actions.append(InspectorAction(rect, "text", field=field))
    return y + height + round(10 * scale)


def draw_enum_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    current: str,
    values: tuple[str, ...],
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    y = draw_text(screen, font, f"{field}:", x, y, (188, 196, 204))
    button_width = max(round(72 * scale), width // len(values) - round(5 * scale))
    height = round(28 * scale)
    bx = x
    for value in values:
        rect = pygame.Rect(bx, y, button_width, height)
        color = (68, 88, 96) if value == current else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(value, True, (224, 228, 232)), (bx + round(7 * scale), y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, "set", field=field, value=value))
        bx += button_width + round(6 * scale)
    return y + height + round(10 * scale)


def draw_button_row(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, buttons: list[tuple[str, str]], x: int, y: int, width: int, scale: float) -> int:
    button_width = max(round(110 * scale), width // max(1, len(buttons)) - round(5 * scale))
    height = round(28 * scale)
    bx = x
    for kind, label in buttons:
        rect = pygame.Rect(bx, y, button_width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(label, True, (224, 228, 232)), (bx + round(7 * scale), y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, kind))
        bx += button_width + round(6 * scale)
    return y + height + round(10 * scale)


def draw_variant_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    current: int,
    count: int,
    x: int,
    y: int,
    width: int,
    scale: float,
    kind: str,
    field: str = "",
) -> int:
    if count <= 1:
        return y

    y = draw_text(screen, font, "variant:", x, y, (188, 196, 204))
    labels = ["auto"] + [str(index) for index in range(count)]
    button_width = max(round(50 * scale), width // len(labels) - round(5 * scale))
    height = round(28 * scale)
    bx = x
    for label in labels:
        value = -1 if label == "auto" else int(label)
        rect = pygame.Rect(bx, y, button_width, height)
        color = (68, 88, 96) if value == current else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(label, True, (224, 228, 232)), (bx + round(7 * scale), y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, kind, field=field, value=value))
        bx += button_width + round(6 * scale)
    return y + height + round(10 * scale)


def draw_validation_summary(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, issues: list[Any], x: int, y: int, width: int, scale: float) -> int:
    errors = sum(1 for issue in issues if issue.severity == "error")
    warnings = sum(1 for issue in issues if issue.severity == "warning")
    y = draw_text(screen, font, f"Validation: {errors} errors, {warnings} warnings", x, y, (230, 196, 142) if issues else (142, 210, 166))
    height = round(28 * scale)
    for issue in issues[:8]:
        rect = pygame.Rect(x, y, width, height)
        color = (86, 38, 42) if issue.severity == "error" else (84, 70, 36)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        text = issue.message if len(issue.message) <= 38 else issue.message[:35] + "..."
        screen.blit(font.render(text, True, (238, 230, 214)), (x + round(7 * scale), y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, "issue", target=issue.target))
        y += height + round(5 * scale)
    return y


def target_title(kind: str, target: dict) -> str:
    if kind == "object":
        return f"Object: {target.get('type', 'unknown')}"
    if kind == "enemy":
        return "Enemy: announcer"
    if kind == "player":
        return "Player start"
    if kind == "exit":
        return "Exit"
    return kind


def direction_angle(target: dict) -> float:
    direction = target.get("direction") or {"x": 0.0, "y": -1.0}
    x = float(direction.get("x", 0.0))
    y = float(direction.get("y", -1.0))
    return math.degrees(math.atan2(y, x))


def rotate_direction(target: dict, degrees: float) -> None:
    direction = target.get("direction") or {"x": 0.0, "y": -1.0}
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
    target["direction"] = {
        "x": round(x * cos_a - y * sin_a, 4),
        "y": round(x * sin_a + y * cos_a, 4),
    }


def display_text_value(value: object) -> str:
    if value is None:
        return ""
    if isinstance(value, list):
        return ", ".join(str(item) for item in value)
    return str(value)


def draw_field_value(screen: pygame.Surface, font: pygame.font.Font, value: str, rect: pygame.Rect, scale: float) -> None:
    x = rect.x + round(8 * scale)
    y = rect.y + round(6 * scale)
    max_lines = max(1, (rect.height - round(8 * scale)) // max(1, font.get_height()))
    lines = value.splitlines() or [""]
    for line in lines[:max_lines]:
        clipped = line if len(line) <= 38 else line[:35] + "..."
        screen.blit(font.render(clipped, True, (230, 234, 238)), (x, y))
        y += font.get_height() + 2


def coerce_text_value(field: str, value: str) -> object:
    if field in ("requiresPlates", "requiresStories"):
        return [part.strip() for part in value.split(",") if part.strip()]
    return value


def is_allowed_text(value: str, field: str) -> bool:
    if field == "text":
        return all(ch >= " " for ch in value)
    if field in ("requiresPlates", "requiresStories"):
        return all(ch.isalnum() or ch in "_-., " for ch in value)
    return all(ch.isalnum() or ch in "_-." for ch in value)


def inside_tiles(tiles: list, cell: tuple[int, int]) -> bool:
    x, y = cell
    return 0 <= x < len(tiles) and len(tiles) > 0 and 0 <= y < len(tiles[x])
