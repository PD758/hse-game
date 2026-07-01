from __future__ import annotations

import json
import math
from dataclasses import dataclass
from typing import Any, Callable

import pygame


BRANCHES = ("none", "puzzle", "combat")
FRAMES = ("vertical", "horizontal")
EVENT_TRIGGERS = ("levelStart", "enterRegion", "statsChanged", "enemyKilled", "enemyGroupCleared")
STAT_OPS = ("ge", "gt", "le", "lt", "eq", "ne")
STAT_NAMES = ("enemiesKilled", "enemiesKilledOnLevel", "camerasBroken", "currentRating")


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
        self.text = display_text_value(value)

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
        y = draw_event_list(screen, font, state, level, x + margin, y + round(8 * scale), width - margin * 2, scale)
        draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(14 * scale), width - margin * 2, scale)
        return

    target = target_for_ref(level, selected_ref)
    if target is None:
        draw_text(screen, font, "Nothing selected", x + margin, y, (188, 196, 204))
        return

    options = collect_level_options(level)
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
            y = draw_dependency_picker(screen, font, state, "requiresPlates", target.get("requiresPlates", []), options["plates"], x + margin, y, width - margin * 2, scale)
            y = draw_dependency_picker(screen, font, state, "requiresStories", target.get("requiresStories", []), options["stories"], x + margin, y, width - margin * 2, scale)
            y = draw_dependency_picker(screen, font, state, "requiresEnemies", target.get("requiresEnemies", []), options["enemies"], x + margin, y, width - margin * 2, scale)
            y = draw_stat_condition_editor(screen, font, state, "requiresStats", target.get("requiresStats", []), x + margin, y, width - margin * 2, scale)
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
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "group", target.get("group", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "level", target.get("level", 3), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "hp", target.get("hp", 2), x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "branch", target.get("branch", "none"), BRANCHES, x + margin, y, width - margin * 2, scale)
        patrol = target.setdefault("patrol", [])
        y = draw_text(screen, font, f"patrol points: {len(patrol)}", x + margin, y, (188, 196, 204))
        y = draw_button_row(screen, font, state, [("remove_patrol", "Remove last"), ("clear_patrol", "Clear")], x + margin, y, width - margin * 2, scale)
        if patrol_mode:
            y = draw_text(screen, font, "Patrol edit: click map to add", x + margin, y, (166, 212, 230))
    elif kind == "exit":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "targetLevel", target.get("targetLevel", ""), x + margin, y, width - margin * 2, scale)
        y = draw_choice_buttons(screen, font, state, "requiresGate", target.get("requiresGate", ""), [""] + options["gates"], x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "branch", target.get("branch", "none"), BRANCHES, x + margin, y, width - margin * 2, scale)
        y = draw_text(screen, font, "Cursor-drag to move.", x + margin, y, (188, 196, 204))
    elif kind == "player":
        y = draw_text(screen, font, "Cursor-drag to move.", x + margin, y, (188, 196, 204))
    elif kind == "event":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "trigger", target.get("trigger", "enterRegion"), EVENT_TRIGGERS, x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "once", str(bool(target.get("once", True))).lower(), ("true", "false"), x + margin, y, width - margin * 2, scale)
        trigger = target.get("trigger", "enterRegion")
        if trigger == "enterRegion":
            y = draw_choice_buttons(screen, font, state, "region", target.get("region", ""), [""] + options["regions"], x + margin, y, width - margin * 2, scale)
        elif trigger == "enemyKilled":
            y = draw_choice_buttons(screen, font, state, "enemyId", target.get("enemyId", ""), [""] + options["enemies"], x + margin, y, width - margin * 2, scale)
        elif trigger == "enemyGroupCleared":
            y = draw_choice_buttons(screen, font, state, "enemyGroup", target.get("enemyGroup", ""), [""] + options["enemyGroups"], x + margin, y, width - margin * 2, scale)
        y = draw_stat_condition_editor(screen, font, state, "conditions", target.get("conditions", []), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "actions", target.get("actions", []), x + margin, y, width - margin * 2, scale, multiline=True)
        y = draw_button_row(screen, font, state, [("delete_event", "Delete event")], x + margin, y, width - margin * 2, scale)

    draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(16 * scale), width - margin * 2, scale)


def handle_key(state: InspectorState, level: dict, selected_ref: tuple[str, int] | None, event: pygame.event.Event) -> bool:
    if not state.active_field or state.active_ref is None or state.active_ref != selected_ref:
        state.clear_text()
        return False

    if event.key in (pygame.K_RETURN, pygame.K_KP_ENTER) and state.active_field in ("text", "actions", "conditions", "requiresStats") and not pygame.key.get_mods() & pygame.KMOD_CTRL:
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
        if action.kind == "select_ref":
            state.pending_select_ref = action.target
            state.clear_text()
            return False, current_tile_variant
        if action.kind == "add_event":
            events = level.setdefault("events", [])
            events.append({"id": f"event_{len(events) + 1}", "enabled": True, "once": True, "trigger": "enterRegion", "region": "", "enemyId": "", "enemyGroup": "", "conditions": [], "actions": []})
            state.pending_select_ref = ("event", len(events) - 1)
            state.clear_text()
            return True, current_tile_variant

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
            target[action.field] = coerce_action_value(action.field, action.value)
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "toggle_list_value":
            values = target.setdefault(action.field, [])
            value = str(action.value)
            if value in values:
                values.remove(value)
            elif value:
                values.append(value)
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "add_stat_condition":
            conditions = target.setdefault(action.field, [])
            conditions.append(["ge", "enemiesKilledOnLevel", 1])
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "remove_stat_condition":
            conditions = target.setdefault(action.field, [])
            index = condition_action_index(action)
            if 0 <= index < len(conditions):
                del conditions[index]
                state.clear_text()
                return True, current_tile_variant
            return False, current_tile_variant
        if action.kind == "cycle_stat_op":
            return cycle_stat_condition(target, action.field, condition_action_index(action), 0, STAT_OPS, state), current_tile_variant
        if action.kind == "cycle_stat_name":
            return cycle_stat_condition(target, action.field, condition_action_index(action), 1, STAT_NAMES, state), current_tile_variant
        if action.kind == "adjust_stat_value":
            conditions = target.setdefault(action.field, [])
            index = condition_action_index(action)
            if 0 <= index < len(conditions) and is_stat_condition(conditions[index]):
                current = parse_number(str(conditions[index][2]))
                if not isinstance(current, (int, float)):
                    current = 0
                conditions[index][2] = clamp_condition_value(conditions[index][1], current + action.amount)
                state.clear_text()
                return True, current_tile_variant
            return False, current_tile_variant
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
        if action.kind == "delete_event":
            if selected_ref is not None and selected_ref[0] == "event":
                events = level.setdefault("events", [])
                index = selected_ref[1]
                if 0 <= index < len(events):
                    del events[index]
                    state.pending_select_ref = None
                    state.clear_text()
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
    if kind == "event" and 0 <= index < len(level.get("events", [])):
        return level["events"][index]
    return None


def draw_event_list(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, level: dict, x: int, y: int, width: int, scale: float) -> int:
    events = level.setdefault("events", [])
    y = draw_text(screen, font, f"Events: {len(events)}", x, y, (166, 212, 230))
    y = draw_button_row(screen, font, state, [("add_event", "Add event")], x, y, width, scale)
    height = round(26 * scale)
    for index, event in enumerate(events[:6]):
        rect = pygame.Rect(x, y, width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        label = f"{event.get('id', f'event_{index}')} [{event.get('trigger', '')}]"
        screen.blit(font.render(label[:38], True, (224, 228, 232)), (x + round(7 * scale), y + round(5 * scale)))
        state.actions.append(InspectorAction(rect, "select_ref", target=("event", index)))
        y += height + round(5 * scale)
    return y


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


def draw_choice_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    current: object,
    values: list[str],
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    y = draw_text(screen, font, f"{field}:", x, y, (188, 196, 204))
    normalized = unique_strings(values)
    if any(str(value).strip() == "" for value in values):
        normalized = [""] + normalized
    if not normalized:
        normalized = [""]
    return draw_wrapped_buttons(screen, font, state, [(value, value or "none", "set") for value in normalized], str(current or ""), field, x, y, width, scale)


def draw_dependency_picker(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    current: object,
    candidates: list[str],
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    values = [str(item) for item in current or [] if str(item)]
    y = draw_text(screen, font, f"{field}:", x, y, (188, 196, 204))
    if values:
        y = draw_wrapped_buttons(screen, font, state, [(value, f"- {value}", "toggle_list_value") for value in values], "", field, x, y, width, scale)
    available = [value for value in unique_strings(candidates) if value not in values]
    if available:
        y = draw_wrapped_buttons(screen, font, state, [(value, f"+ {value}", "toggle_list_value") for value in available], "", field, x, y, width, scale)
    elif not values:
        y = draw_text(screen, font, "No matching ids yet", x, y, (132, 140, 148))
    return y + round(4 * scale)


def draw_wrapped_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    buttons: list[tuple[str, str, str]],
    current: str,
    field: str,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    gap = round(6 * scale)
    height = round(28 * scale)
    bx = x
    row_y = y
    for value, label, kind in buttons:
        shown = label if len(label) <= 18 else label[:15] + "..."
        button_width = min(width, max(round(58 * scale), font.size(shown)[0] + round(16 * scale)))
        if bx + button_width > x + width and bx > x:
            bx = x
            row_y += height + gap
        rect = pygame.Rect(bx, row_y, button_width, height)
        color = (68, 88, 96) if str(value) == current else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(shown, True, (224, 228, 232)), (bx + round(7 * scale), row_y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, kind, field=field, value=value))
        bx += button_width + gap
    return row_y + height + round(8 * scale)


def draw_stat_condition_editor(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    conditions: object,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    y = draw_text(screen, font, f"{field}:", x, y, (188, 196, 204))
    rows = [(index, condition) for index, condition in enumerate(conditions or []) if is_stat_condition(condition)]
    if not rows:
        y = draw_text(screen, font, "No stat conditions", x, y, (132, 140, 148))
    for index, condition in rows:
        y = draw_stat_condition_row(screen, font, state, field, index, condition, x, y, width, scale)
    height = round(28 * scale)
    rect = pygame.Rect(x, y, width, height)
    pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
    screen.blit(font.render("+ condition", True, (224, 228, 232)), (x + round(7 * scale), y + round(6 * scale)))
    state.actions.append(InspectorAction(rect, "add_stat_condition", field=field))
    return y + height + round(10 * scale)


def draw_stat_condition_row(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    index: int,
    condition: list,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    height = round(28 * scale)
    gap = round(5 * scale)
    op = str(condition[0])
    stat = str(condition[1])
    value = condition[2]
    specs = [
        ("cycle_stat_op", op, round(38 * scale), 0.0),
        ("cycle_stat_name", short_stat_name(stat), round(118 * scale), 0.0),
        ("adjust_stat_value", "-10", round(38 * scale), -10.0),
        ("adjust_stat_value", "-1", round(32 * scale), -1.0),
        ("adjust_stat_value", str(value), round(46 * scale), 0.0),
        ("adjust_stat_value", "+1", round(32 * scale), 1.0),
        ("adjust_stat_value", "+10", round(38 * scale), 10.0),
        ("remove_stat_condition", "x", round(28 * scale), 0.0),
    ]
    bx = x
    row_y = y
    for kind, label, button_width, amount in specs:
        if bx + button_width > x + width and bx > x:
            bx = x
            row_y += height + gap
        rect = pygame.Rect(bx, row_y, button_width, height)
        color = (48, 54, 62) if amount == 0.0 else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(label, True, (224, 228, 232)), (bx + round(7 * scale), row_y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, kind, field=field, amount=amount, target=("condition", index)))
        bx += button_width + gap
    return row_y + height + round(6 * scale)


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
    gap = round(6 * scale)
    button_width = max(round(44 * scale), min(round(58 * scale), width // min(len(labels), 5) - gap))
    height = round(28 * scale)
    bx = x
    row_y = y
    for label in labels:
        if bx + button_width > x + width and bx > x:
            bx = x
            row_y += height + gap
        value = -1 if label == "auto" else int(label)
        rect = pygame.Rect(bx, row_y, button_width, height)
        color = (68, 88, 96) if value == current else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
        screen.blit(font.render(label, True, (224, 228, 232)), (bx + round(7 * scale), row_y + round(6 * scale)))
        state.actions.append(InspectorAction(rect, kind, field=field, value=value))
        bx += button_width + gap
    return row_y + height + round(10 * scale)


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


def collect_level_options(level: dict) -> dict[str, list[str]]:
    plates: list[str] = []
    stories: list[str] = []
    gates: list[str] = []
    enemies: list[str] = []
    enemy_groups: list[str] = []
    regions: list[str] = []
    for obj in level.get("objects", []) or []:
        obj_type = obj.get("type", "")
        if obj_type == "plate":
            plates.append(str(obj.get("group", "")))
        elif obj_type == "story":
            stories.append(str(obj.get("id", "")))
        elif obj_type == "gate":
            gates.append(str(obj.get("id", "")))
    for enemy in level.get("enemies", []) or []:
        enemies.append(str(enemy.get("id", "")))
        enemy_groups.append(str(enemy.get("group", "")))
    for event in level.get("events", []) or []:
        for action in event.get("actions", []) or []:
            if isinstance(action, dict):
                if action.get("type") in ("spawnEnemy", "spawnEnemies"):
                    enemies.append(str(action.get("id", "")))
                    enemy_groups.append(str(action.get("group", "")))
    for region in level.get("regions", []) or []:
        regions.append(str(region.get("id", "")))
    return {
        "plates": unique_strings(plates),
        "stories": unique_strings(stories),
        "gates": unique_strings(gates),
        "enemies": unique_strings(enemies),
        "enemyGroups": unique_strings(enemy_groups),
        "regions": unique_strings(regions),
    }


def unique_strings(values: list[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        normalized = str(value).strip()
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        result.append(normalized)
    return result


def condition_action_index(action: InspectorAction) -> int:
    if action.target is None:
        return -1
    return int(action.target[1])


def cycle_stat_condition(target: dict, field: str, index: int, part_index: int, values: tuple[str, ...], state: InspectorState) -> bool:
    conditions = target.setdefault(field, [])
    if not (0 <= index < len(conditions)) or not is_stat_condition(conditions[index]):
        return False
    current = str(conditions[index][part_index])
    try:
        next_index = (values.index(current) + 1) % len(values)
    except ValueError:
        next_index = 0
    conditions[index][part_index] = values[next_index]
    if part_index == 1:
        conditions[index][2] = clamp_condition_value(conditions[index][1], parse_number(str(conditions[index][2])))
    state.clear_text()
    return True


def clamp_condition_value(stat_name: object, value: object) -> int | float:
    if not isinstance(value, (int, float)):
        value = 0
    if stat_name == "currentRating":
        return round(max(0, min(100, float(value))), 2)
    return max(0, int(round(float(value))))


def short_stat_name(stat_name: str) -> str:
    aliases = {
        "enemiesKilled": "kills",
        "enemiesKilledOnLevel": "levelKills",
        "camerasBroken": "cameras",
        "currentRating": "rating",
    }
    return aliases.get(stat_name, stat_name[:10])


def target_title(kind: str, target: dict) -> str:
    if kind == "object":
        return f"Object: {target.get('type', 'unknown')}"
    if kind == "enemy":
        return "Enemy: announcer"
    if kind == "player":
        return "Player start"
    if kind == "exit":
        return "Exit"
    if kind == "event":
        return f"Event: {target.get('id', 'event')}"
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
        if value and all(isinstance(item, dict) for item in value):
            return json.dumps(value, ensure_ascii=False, indent=2)
        if all(is_stat_condition(item) for item in value):
            return "; ".join(format_stat_condition(item) for item in value)
        return ", ".join(str(item) for item in value)
    return str(value)


def is_stat_condition(value: object) -> bool:
    return isinstance(value, list) and len(value) >= 3


def format_stat_condition(value: list) -> str:
    return f"{value[0]} {value[1]} {value[2]}"


def draw_field_value(screen: pygame.Surface, font: pygame.font.Font, value: str, rect: pygame.Rect, scale: float) -> None:
    x = rect.x + round(8 * scale)
    y = rect.y + round(6 * scale)
    max_width = max(10, rect.width - round(16 * scale))
    max_lines = max(1, (rect.height - round(8 * scale)) // max(1, font.get_height()))
    lines = wrap_text(value, font, max_width)
    for line in lines[:max_lines]:
        screen.blit(font.render(line, True, (230, 234, 238)), (x, y))
        y += font.get_height() + 2


def wrap_text(value: str, font: pygame.font.Font, max_width: int) -> list[str]:
    source_lines = value.splitlines() or [""]
    result: list[str] = []
    for source in source_lines:
        if not source:
            result.append("")
            continue
        current = ""
        for word in source.split(" "):
            candidate = word if not current else f"{current} {word}"
            if font.size(candidate)[0] <= max_width:
                current = candidate
                continue
            if current:
                result.append(current)
            current = fit_word(word, font, max_width, result)
        result.append(current)
    return result


def fit_word(word: str, font: pygame.font.Font, max_width: int, result: list[str]) -> str:
    if font.size(word)[0] <= max_width:
        return word

    current = ""
    for char in word:
        candidate = current + char
        if current and font.size(candidate)[0] > max_width:
            result.append(current)
            current = char
        else:
            current = candidate
    return current


def coerce_text_value(field: str, value: str) -> object:
    if field in ("requiresPlates", "requiresStories", "requiresEnemies"):
        return [part.strip() for part in value.split(",") if part.strip()]
    if field in ("requiresStats", "conditions"):
        return parse_stat_conditions(value)
    if field == "actions":
        try:
            parsed = json.loads(value)
            return parsed if isinstance(parsed, list) else []
        except json.JSONDecodeError:
            return []
    if field == "level":
        try:
            return min(9, max(1, int(value)))
        except ValueError:
            return 3
    if field == "hp":
        try:
            return max(1, int(value))
        except ValueError:
            return 2
    return value


def is_allowed_text(value: str, field: str) -> bool:
    if field in ("text", "actions"):
        return all(ch >= " " for ch in value)
    if field in ("requiresPlates", "requiresStories", "requiresEnemies"):
        return all(ch.isalnum() or ch in "_-., " for ch in value)
    if field in ("requiresStats", "conditions"):
        return all(ch.isalnum() or ch in "_-.,;()[] " for ch in value)
    if field == "level":
        return value.isdigit()
    if field == "hp":
        return value.isdigit()
    return all(ch.isalnum() or ch in "_-." for ch in value)


def coerce_action_value(field: str, value: object) -> object:
    if field == "once":
        return value == "true" or value is True
    return value


def inside_tiles(tiles: list, cell: tuple[int, int]) -> bool:
    x, y = cell
    return 0 <= x < len(tiles) and len(tiles) > 0 and 0 <= y < len(tiles[x])


def parse_stat_conditions(value: str) -> list[list[object]]:
    result: list[list[object]] = []
    for raw_part in value.replace("\n", ";").split(";"):
        part = raw_part.strip().strip("()[]")
        if not part:
            continue
        tokens = [token.strip() for token in part.replace(",", " ").split() if token.strip()]
        if len(tokens) < 3:
            continue
        op, stat_name, target = tokens[0], tokens[1], tokens[2]
        result.append([op, stat_name, parse_number(target)])
    return result


def parse_number(value: str) -> int | float | str:
    try:
        number = float(value)
    except ValueError:
        return value
    if number.is_integer():
        return int(number)
    return number
