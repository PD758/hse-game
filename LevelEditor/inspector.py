from __future__ import annotations

import json
import math
from dataclasses import dataclass
from typing import Any, Callable

import pygame


BRANCHES = ("none", "puzzle", "combat")
ENEMY_ARCHETYPES = ("patrol", "hunter", "brute", "caller")
FRAMES = ("vertical", "horizontal")
BOOLS = ("true", "false")
LIGHT_TYPES = ("point", "cone")
EVENT_TRIGGERS = ("levelStart", "enterRegion", "statsChanged", "enemyKilled", "enemyGroupCleared")
STAT_OPS = ("ge", "gt", "le", "lt", "eq", "ne")
STAT_NAMES = ("enemiesKilled", "enemiesKilledOnLevel", "camerasBroken", "currentRating")
ACTION_TYPES = ("showMonologue", "fallStone", "spawnEnemy", "setTile", "spawnObject", "removeObject", "playEffect")
ACTION_TILES = ("floor", "wall", "rubble")
ACTION_OBJECTS = ("gate", "remote", "flashlight", "trap", "story", "storyImage", "heal", "plate", "stone", "rubble")


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
        self.cursor_index = 0
        self.actions: list[InspectorAction] = []
        self.pending_select_ref: tuple[str, int] | None = None
        self.scroll_y = 0
        self.content_height = 0

    def begin_text(self, ref: tuple[str, int], field: str, value: object) -> None:
        self.active_ref = ref
        self.active_field = field
        self.text = display_text_value(value)
        self.cursor_index = len(self.text)

    def clear_text(self) -> None:
        self.active_ref = None
        self.active_field = ""
        self.text = ""
        self.cursor_index = 0

    def reset_scroll(self) -> None:
        self.scroll_y = 0

    def scroll(self, wheel_delta: int, visible_height: int, scale: float) -> None:
        step = max(24, round(56 * scale))
        self.scroll_y -= wheel_delta * step
        self.clamp_scroll(visible_height)

    def clamp_scroll(self, visible_height: int) -> None:
        max_scroll = max(0, self.content_height - visible_height)
        self.scroll_y = max(0, min(self.scroll_y, max_scroll))


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
    panel = pygame.Rect(x, 0, width, screen.get_height())
    pygame.draw.rect(screen, (22, 24, 28), panel)
    previous_clip = screen.get_clip()
    screen.set_clip(panel)
    y = round(14 * scale) - state.scroll_y

    def finish(final_y: int) -> None:
        content_height = max(panel.height, final_y + state.scroll_y + margin)
        state.content_height = content_height
        state.clamp_scroll(panel.height)
        screen.set_clip(previous_clip)
        draw_inspector_scrollbar(screen, state, panel, scale)

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
        y = draw_region_list(screen, font, state, level, x + margin, y + round(8 * scale), width - margin * 2, scale)
        y = draw_event_list(screen, font, state, level, x + margin, y + round(8 * scale), width - margin * 2, scale)
        y = draw_visual_list(screen, font, state, level, x + margin, y + round(8 * scale), width - margin * 2, scale)
        y = draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(14 * scale), width - margin * 2, scale)
        finish(y)
        return

    target = target_for_ref(level, selected_ref)
    if target is None:
        y = draw_text(screen, font, "Nothing selected", x + margin, y, (188, 196, 204))
        finish(y)
        return

    options = collect_level_options(level)
    kind, _index = selected_ref
    title = target_title(kind, target)
    y = draw_text(screen, font, title, x + margin, y, (166, 212, 230))
    if kind != "region":
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
        elif obj_type == "storyImage":
            y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
            y = draw_text_field(screen, font, state, selected_ref, "imagePath", target.get("imagePath", ""), x + margin, y, width - margin * 2, scale)
        else:
            y = draw_text(screen, font, "No editable properties", x + margin, y, (132, 140, 148))
    elif kind == "enemy":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "group", target.get("group", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "alertGroup", target.get("alertGroup", ""), x + margin, y, width - margin * 2, scale)
        y = draw_enemy_type_buttons(screen, font, state, target.get("type", "patrol"), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "level", target.get("level", 3), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "hp", target.get("hp", 2), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "hearing", target.get("hearing", 0), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "vision", target.get("vision", 0), x + margin, y, width - margin * 2, scale)
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
    elif kind == "decoration":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "texturePath", target.get("texturePath", ""), x + margin, y, width - margin * 2, scale)
        y = draw_number_adjuster(screen, font, state, "scale", target.get("scale", 1.0), (-0.25, -0.05, 0.05, 0.25), x + margin, y, width - margin * 2, scale)
        y = draw_number_adjuster(screen, font, state, "rotation", target.get("rotation", 0), (-15, -5, 5, 15), x + margin, y, width - margin * 2, scale)
        y = draw_number_adjuster(screen, font, state, "sortingOrder", target.get("sortingOrder", 4), (-10, -1, 1, 10), x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "castsShadow", str(bool(target.get("castsShadow", False))).lower(), BOOLS, x + margin, y, width - margin * 2, scale)
        y = draw_text(screen, font, "Cursor-drag to move. Shift-drag snaps to grid.", x + margin, y, (188, 196, 204))
    elif kind == "light":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_enum_buttons(screen, font, state, "type", target.get("type", "point"), LIGHT_TYPES, x + margin, y, width - margin * 2, scale)
        y = draw_text_field(screen, font, state, selected_ref, "color", target.get("color", "#d6f0ff"), x + margin, y, width - margin * 2, scale)
        y = draw_number_adjuster(screen, font, state, "intensity", target.get("intensity", 0.7), (-0.25, -0.05, 0.05, 0.25), x + margin, y, width - margin * 2, scale)
        y = draw_number_adjuster(screen, font, state, "radius", target.get("radius", 4.0), (-1, -0.25, 0.25, 1), x + margin, y, width - margin * 2, scale)
        if target.get("type", "point") == "cone":
            y = draw_number_adjuster(screen, font, state, "rotation", target.get("rotation", 0), (-15, -5, 5, 15), x + margin, y, width - margin * 2, scale)
            y = draw_number_adjuster(screen, font, state, "outerAngle", target.get("outerAngle", 65), (-10, -5, 5, 10), x + margin, y, width - margin * 2, scale)
            y = draw_number_adjuster(screen, font, state, "innerAngle", target.get("innerAngle", 30), (-10, -5, 5, 10), x + margin, y, width - margin * 2, scale)
        y = draw_text(screen, font, "Cursor-drag to move. Shift-drag snaps to grid.", x + margin, y, (188, 196, 204))
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
        y = draw_action_editor(screen, font, state, level, selected_cell, target, x + margin, y, width - margin * 2, scale)
        y = draw_button_row(screen, font, state, [("delete_event", "Delete event")], x + margin, y, width - margin * 2, scale)
    elif kind == "region":
        y = draw_text_field(screen, font, state, selected_ref, "id", target.get("id", ""), x + margin, y, width - margin * 2, scale)
        y = draw_text(screen, font, f"cells: {region_cell_count(target)}", x + margin, y, (188, 196, 204))
        y = draw_text(screen, font, "Regions tool: click cells to toggle", x + margin, y, (166, 212, 230))
        y = draw_text(screen, font, "Drag to add cells", x + margin, y, (132, 140, 148))
        y = draw_button_row(screen, font, state, [("clear_region", "Clear cells"), ("delete_region", "Delete")], x + margin, y, width - margin * 2, scale)

    y = draw_validation_summary(screen, font, state, validation_issues or [], x + margin, y + round(16 * scale), width - margin * 2, scale)
    finish(y)


def handle_key(state: InspectorState, level: dict, selected_ref: tuple[str, int] | None, event: pygame.event.Event) -> bool:
    if not state.active_field or state.active_ref is None or state.active_ref != selected_ref:
        state.clear_text()
        return False

    state.cursor_index = max(0, min(state.cursor_index, len(state.text)))
    if event.key == pygame.K_LEFT:
        state.cursor_index = max(0, state.cursor_index - 1)
        return False
    if event.key == pygame.K_RIGHT:
        state.cursor_index = min(len(state.text), state.cursor_index + 1)
        return False
    if event.key == pygame.K_HOME:
        state.cursor_index = line_boundary(state.text, state.cursor_index, -1)
        return False
    if event.key == pygame.K_END:
        state.cursor_index = line_boundary(state.text, state.cursor_index, 1)
        return False

    if event.key in (pygame.K_RETURN, pygame.K_KP_ENTER) and is_multiline_field(state.active_field) and not pygame.key.get_mods() & pygame.KMOD_CTRL:
        state.text = state.text[:state.cursor_index] + "\n" + state.text[state.cursor_index:]
        state.cursor_index += 1
    elif event.key in (pygame.K_RETURN, pygame.K_KP_ENTER, pygame.K_ESCAPE):
        state.clear_text()
        return False
    elif event.key == pygame.K_BACKSPACE:
        if state.cursor_index <= 0:
            return False
        state.text = state.text[:state.cursor_index - 1] + state.text[state.cursor_index:]
        state.cursor_index -= 1
    elif event.key == pygame.K_DELETE:
        if state.cursor_index >= len(state.text):
            return False
        state.text = state.text[:state.cursor_index] + state.text[state.cursor_index + 1:]
    elif event.unicode and is_allowed_text(event.unicode, state.active_field):
        state.text = state.text[:state.cursor_index] + event.unicode + state.text[state.cursor_index:]
        state.cursor_index += len(event.unicode)
    else:
        return False

    target = target_for_ref(level, state.active_ref)
    if target is not None:
        if state.active_field.startswith("action:"):
            return set_action_text_value(target, state.active_field, state.text)
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
    font: pygame.font.Font,
    scale: float,
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
        if action.kind == "add_region":
            regions = level.setdefault("regions", [])
            regions.append({"id": f"region_{len(regions) + 1}", "runs": []})
            state.pending_select_ref = ("region", len(regions) - 1)
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
            was_active = state.active_ref == selected_ref and state.active_field == action.field
            visible_cursor_index = state.cursor_index if was_active else None
            state.begin_text(selected_ref, action.field, target.get(action.field, ""))
            state.cursor_index = cursor_index_from_pos(state.text, action.rect, pos, font, scale, visible_cursor_index)
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
        if action.kind == "adjust_number":
            target[action.field] = clamp_number_field(action.field, parse_number(str(target.get(action.field, 0))) if target.get(action.field, "") != "" else 0, action.amount)
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "add_monologue":
            target.setdefault("actions", []).append({"type": "showMonologue", "text": "Я должен понять, где оказался, и найти выход из эфира."})
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "add_action":
            target.setdefault("actions", []).append({"type": "fallStone", "x": 0, "y": 0})
            state.clear_text()
            return True, current_tile_variant
        if action.kind == "add_region_fallstones":
            return add_region_fallstone_actions(level, target, state), current_tile_variant
        if action.kind == "delete_action":
            return delete_action_at(target, action_index(action), state), current_tile_variant
        if action.kind == "cycle_action_type":
            return cycle_action_value(target, action_index(action), "type", ACTION_TYPES, state), current_tile_variant
        if action.kind == "cycle_action_tile":
            return cycle_action_value(target, action_index(action), "tile", ACTION_TILES, state), current_tile_variant
        if action.kind == "cycle_action_object":
            return cycle_action_value(target, action_index(action), "objectType", ACTION_OBJECTS, state), current_tile_variant
        if action.kind == "cycle_enemy_type":
            return cycle_action_value(target, action_index(action), "objectType", ENEMY_ARCHETYPES, state), current_tile_variant
        if action.kind == "adjust_action_int":
            return adjust_action_int(target, action_index(action), action.field, int(action.amount), state), current_tile_variant
        if action.kind == "action_use_cell":
            return set_action_cell(target, action_index(action), selected_cell, state), current_tile_variant
        if action.kind == "action_text":
            index = action_index(action)
            actions = target.setdefault("actions", [])
            if 0 <= index < len(actions):
                encoded = f"action:{index}:{action.field}"
                was_active = state.active_ref == selected_ref and state.active_field == encoded
                visible_cursor_index = state.cursor_index if was_active else None
                state.begin_text(selected_ref, encoded, actions[index].get(action.field, ""))
                if is_multiline_field(state.active_field):
                    value_rect = pygame.Rect(action.rect.x, action.rect.y + round(24 * scale), action.rect.width, action.rect.height - round(26 * scale))
                    state.cursor_index = cursor_index_from_pos(state.text, value_rect, pos, font, scale, visible_cursor_index)
                else:
                    prefix = f"{action.field}: "
                    prefixed_cursor = len(prefix) + visible_cursor_index if visible_cursor_index is not None else None
                    index_with_prefix = cursor_index_from_pos(f"{prefix}{state.text}", action.rect, pos, font, scale, prefixed_cursor)
                    state.cursor_index = max(0, min(len(state.text), index_with_prefix - len(prefix)))
            return False, current_tile_variant
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
        if action.kind == "clear_region":
            if selected_ref is not None and selected_ref[0] == "region":
                target["runs"] = []
                state.clear_text()
                return True, current_tile_variant
            return False, current_tile_variant
        if action.kind == "delete_region":
            if selected_ref is not None and selected_ref[0] == "region":
                regions = level.setdefault("regions", [])
                index = selected_ref[1]
                if 0 <= index < len(regions):
                    del regions[index]
                    state.pending_select_ref = None
                    state.clear_text()
                    return True, current_tile_variant
            return False, current_tile_variant

    state.clear_text()
    return False, current_tile_variant


def point_inside(screen: pygame.Surface, pos: tuple[int, int], scale: float = 1.0) -> bool:
    return pos[0] >= screen.get_width() - inspector_width(scale)


def draw_inspector_scrollbar(screen: pygame.Surface, state: InspectorState, panel: pygame.Rect, scale: float) -> None:
    if state.content_height <= panel.height:
        return

    track_width = max(4, round(5 * scale))
    track = pygame.Rect(panel.right - track_width - round(4 * scale), round(8 * scale), track_width, panel.height - round(16 * scale))
    pygame.draw.rect(screen, (36, 40, 46), track, border_radius=max(2, track_width // 2))
    thumb_height = max(round(42 * scale), int(track.height * panel.height / max(1, state.content_height)))
    max_scroll = max(1, state.content_height - panel.height)
    thumb_y = track.y + int((track.height - thumb_height) * state.scroll_y / max_scroll)
    pygame.draw.rect(screen, (100, 122, 132), pygame.Rect(track.x, thumb_y, track.width, thumb_height), border_radius=max(2, track_width // 2))


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
    if kind == "decoration" and 0 <= index < len(level.get("decorations", [])):
        return level["decorations"][index]
    if kind == "light" and 0 <= index < len(level.get("lights", [])):
        return level["lights"][index]
    if kind == "event" and 0 <= index < len(level.get("events", [])):
        return level["events"][index]
    if kind == "region" and 0 <= index < len(level.get("regions", [])):
        return level["regions"][index]
    return None


def draw_region_list(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, level: dict, x: int, y: int, width: int, scale: float) -> int:
    regions = level.setdefault("regions", [])
    y = draw_text(screen, font, f"Regions: {len(regions)}", x, y, (166, 212, 230))
    y = draw_button_row(screen, font, state, [("add_region", "Add region")], x, y, width, scale)
    height = round(26 * scale)
    for index, region in enumerate(regions):
        rect = pygame.Rect(x, y, width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        label = f"{region.get('id', f'region_{index + 1}')} ({region_cell_count(region)} cells)"
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        state.actions.append(InspectorAction(rect, "select_ref", target=("region", index)))
        y += height + round(5 * scale)
    return y


def draw_event_list(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, level: dict, x: int, y: int, width: int, scale: float) -> int:
    events = level.setdefault("events", [])
    y = draw_text(screen, font, f"Events: {len(events)}", x, y, (166, 212, 230))
    y = draw_button_row(screen, font, state, [("add_event", "Add event")], x, y, width, scale)
    height = round(26 * scale)
    for index, event in enumerate(events):
        rect = pygame.Rect(x, y, width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        label = f"{event.get('id', f'event_{index}')} [{event.get('trigger', '')}]"
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        state.actions.append(InspectorAction(rect, "select_ref", target=("event", index)))
        y += height + round(5 * scale)
    return y


def draw_visual_list(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, level: dict, x: int, y: int, width: int, scale: float) -> int:
    decorations = level.setdefault("decorations", [])
    lights = level.setdefault("lights", [])
    y = draw_text(screen, font, f"Visuals: {len(decorations)} textures, {len(lights)} lights", x, y, (166, 212, 230))
    height = round(26 * scale)
    for kind, items in (("decoration", decorations), ("light", lights)):
        for index, item in enumerate(items):
            rect = pygame.Rect(x, y, width, height)
            pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
            label = f"{kind}: {item.get('id', f'{kind}_{index + 1}')}"
            draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
            state.actions.append(InspectorAction(rect, "select_ref", target=(kind, index)))
            y += height + round(5 * scale)
    return y


def draw_text(screen: pygame.Surface, font: pygame.font.Font, text: str, x: int, y: int, color: tuple[int, int, int], max_width: int | None = None) -> int:
    clip = screen.get_clip()
    if max_width is None and clip:
        max_width = max(4, clip.right - x - 8)
    label = ellipsize(text, font, max_width) if max_width is not None else text
    screen.blit(font.render(label, True, color), (x, y))
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
    height = round((132 if multiline else 30) * scale)
    rect = pygame.Rect(x, y, width, height)
    active = state.active_ref == ref and state.active_field == field
    color = (58, 66, 74) if active else (36, 40, 46)
    pygame.draw.rect(screen, color, rect, border_radius=max(2, round(4 * scale)))
    if active:
        pygame.draw.rect(screen, (132, 214, 238), rect, max(1, round(2 * scale)), border_radius=max(2, round(4 * scale)))
    shown = state.text if active else display_text_value(value)
    draw_field_value(screen, font, shown, rect, scale, active=active, cursor_index=state.cursor_index if active else None)
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
        draw_clipped_text(screen, font, value, (224, 228, 232), rect, scale)
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


def draw_enemy_type_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    current: str,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    y = draw_text(screen, font, "type:", x, y, (188, 196, 204))
    return draw_wrapped_buttons(screen, font, state, [(value, value, "set") for value in ENEMY_ARCHETYPES], current or "patrol", "type", x, y, width, scale)


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
        draw_clipped_text(screen, font, shown, (224, 228, 232), rect, scale)
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
    draw_clipped_text(screen, font, "+ condition", (224, 228, 232), rect, scale)
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
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        state.actions.append(InspectorAction(rect, kind, field=field, amount=amount, target=("condition", index)))
        bx += button_width + gap
    return row_y + height + round(6 * scale)


def draw_action_editor(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    level: dict,
    selected_cell: tuple[int, int] | None,
    event: dict,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    actions = event.setdefault("actions", [])
    y = draw_text(screen, font, f"actions: {len(actions)}", x, y, (188, 196, 204))
    y = draw_button_row(screen, font, state, [("add_monologue", "+ monologue"), ("add_action", "+ action")], x, y, width, scale)
    y = draw_button_row(screen, font, state, [("add_region_fallstones", "+ stones in region")], x, y, width, scale)
    if event.get("trigger") == "enterRegion" and event.get("region"):
        y = draw_text(screen, font, f"region source: {event.get('region')}", x, y, (132, 174, 188))
    elif actions:
        y = draw_text(screen, font, "Select event region to bulk-add stones", x, y, (132, 140, 148))

    for index, action in enumerate(actions[:8]):
        y = draw_action_row(screen, font, state, selected_cell, index, action, x, y, width, scale)
    if len(actions) > 8:
        y = draw_text(screen, font, f"{len(actions) - 8} more actions in JSON", x, y, (132, 140, 148))
    return y + round(4 * scale)


def draw_action_row(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    selected_cell: tuple[int, int] | None,
    index: int,
    action: dict,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    rect = pygame.Rect(x, y, width, round(30 * scale))
    pygame.draw.rect(screen, (31, 36, 42), rect, border_radius=max(2, round(4 * scale)))
    label = f"#{index + 1} {action.get('type', 'fallStone')}"
    draw_clipped_text(screen, font, label, (224, 228, 232), pygame.Rect(x, y, width - round(34 * scale), rect.height), scale)
    state.actions.append(InspectorAction(pygame.Rect(rect.right - round(32 * scale), y, round(32 * scale), rect.height), "delete_action", target=("action", index)))
    draw_clipped_text(screen, font, "x", (238, 190, 190), pygame.Rect(rect.right - round(32 * scale), y, round(32 * scale), rect.height), scale)
    y += rect.height + round(5 * scale)

    y = draw_action_buttons(screen, font, state, index, [("cycle_action_type", "type", 0)], x, y, width, scale)
    if action_uses_cell(action):
        y = draw_action_buttons(screen, font, state, index, [("action_use_cell", "use selected cell", 0)], x, y, width, scale)
        y = draw_action_coordinate_row(screen, font, state, index, action, selected_cell, x, y, width, scale)

    action_type = action.get("type", "fallStone")
    if action_type == "showMonologue":
        y = draw_text(screen, font, "Shows player monologue in the top story band.", x, y, (132, 174, 188))
        y = draw_action_text_field(screen, font, state, index, "text", action.get("text", ""), x, y, width, scale, multiline=True)
    elif action_type == "setTile":
        y = draw_action_buttons(screen, font, state, index, [("cycle_action_tile", f"tile:{action.get('tile', 'floor')}", 0)], x, y, width, scale)
        y = draw_action_buttons(screen, font, state, index, [("adjust_action_int", "variant -1", -1), ("adjust_action_int", "variant +1", 1)], x, y, width, scale, field="variant")
    elif action_type == "spawnObject":
        y = draw_action_buttons(screen, font, state, index, [("cycle_action_object", f"obj:{action.get('objectType', 'plate')}", 0)], x, y, width, scale)
        y = draw_action_text_field(screen, font, state, index, "id", action.get("id", ""), x, y, width, scale)
        y = draw_action_text_field(screen, font, state, index, "group", action.get("group", ""), x, y, width, scale)
    elif action_type == "spawnEnemy":
        y = draw_action_buttons(screen, font, state, index, [("cycle_enemy_type", f"enemy:{action.get('objectType', 'patrol')}", 0)], x, y, width, scale)
        y = draw_action_text_field(screen, font, state, index, "id", action.get("id", ""), x, y, width, scale)
        y = draw_action_text_field(screen, font, state, index, "group", action.get("group", ""), x, y, width, scale)
        y = draw_action_buttons(screen, font, state, index, [("adjust_action_int", "level -1", -1), ("adjust_action_int", "level +1", 1), ("adjust_action_int", "hp -1", -1), ("adjust_action_int", "hp +1", 1)], x, y, width, scale)
    elif action_type == "removeObject":
        y = draw_action_text_field(screen, font, state, index, "id", action.get("id", ""), x, y, width, scale)
    return y + round(6 * scale)


def draw_action_coordinate_row(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    index: int,
    action: dict,
    selected_cell: tuple[int, int] | None,
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    cell_label = f"@ {int(action.get('x', 0))},{int(action.get('y', 0))}"
    if selected_cell is not None:
        cell_label += f"  sel {selected_cell[0]},{selected_cell[1]}"
    y = draw_text(screen, font, cell_label, x, y, (132, 174, 188))
    y = draw_action_buttons(screen, font, state, index, [("adjust_action_int", "x-1", -1), ("adjust_action_int", "x+1", 1), ("adjust_action_int", "y-1", -1), ("adjust_action_int", "y+1", 1)], x, y, width, scale)
    return y


def draw_action_buttons(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    index: int,
    buttons: list[tuple[str, str, int]],
    x: int,
    y: int,
    width: int,
    scale: float,
    field: str = "",
) -> int:
    gap = round(5 * scale)
    height = round(26 * scale)
    bx = x
    for kind, label, amount in buttons:
        button_width = min(width, max(round(52 * scale), font.size(label)[0] + round(14 * scale)))
        if bx + button_width > x + width and bx > x:
            bx = x
            y += height + gap
        rect = pygame.Rect(bx, y, button_width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        action_field = field
        if kind == "adjust_action_int" and not action_field:
            action_field = label.split()[0][0] if label.startswith(("x", "y")) else label.split()[0]
        state.actions.append(InspectorAction(rect, kind, field=action_field, amount=amount, target=("action", index)))
        bx += button_width + gap
    return y + height + round(5 * scale)


def draw_action_text_field(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    index: int,
    field: str,
    value: object,
    x: int,
    y: int,
    width: int,
    scale: float,
    multiline: bool = False,
) -> int:
    height = round((138 if multiline else 28) * scale)
    rect = pygame.Rect(x, y, width, height)
    encoded = f"action:{index}:{field}"
    active = state.active_field == encoded
    pygame.draw.rect(screen, (58, 66, 74) if active else (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
    if active:
        pygame.draw.rect(screen, (132, 214, 238), rect, max(1, round(2 * scale)), border_radius=max(2, round(4 * scale)))
    shown = state.text if active else str(value or "")
    if multiline:
        draw_clipped_text(screen, font, f"{field}:", (166, 212, 230), pygame.Rect(x, y, width, round(24 * scale)), scale)
        draw_field_value(screen, font, shown, pygame.Rect(x, y + round(24 * scale), width, height - round(26 * scale)), scale, active=active, cursor_index=state.cursor_index if active else None)
    else:
        prefix = f"{field}: "
        cursor_index = len(prefix) + state.cursor_index if active else None
        draw_field_value(screen, font, f"{prefix}{shown}", rect, scale, active=active, cursor_index=cursor_index)
    state.actions.append(InspectorAction(rect, "action_text", field=field, target=("action", index)))
    return y + height + round(5 * scale)


def draw_button_row(screen: pygame.Surface, font: pygame.font.Font, state: InspectorState, buttons: list[tuple[str, str]], x: int, y: int, width: int, scale: float) -> int:
    button_width = max(round(110 * scale), width // max(1, len(buttons)) - round(5 * scale))
    height = round(28 * scale)
    bx = x
    for kind, label in buttons:
        rect = pygame.Rect(bx, y, button_width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        state.actions.append(InspectorAction(rect, kind))
        bx += button_width + round(6 * scale)
    return y + height + round(10 * scale)


def draw_number_adjuster(
    screen: pygame.Surface,
    font: pygame.font.Font,
    state: InspectorState,
    field: str,
    current: object,
    steps: tuple[float, ...],
    x: int,
    y: int,
    width: int,
    scale: float,
) -> int:
    y = draw_text(screen, font, f"{field}: {format_number(current)}", x, y, (188, 196, 204))
    gap = round(5 * scale)
    height = round(26 * scale)
    bx = x
    for step in steps:
        label = signed_number(step)
        button_width = max(round(48 * scale), font.size(label)[0] + round(16 * scale))
        if bx + button_width > x + width and bx > x:
            bx = x
            y += height + gap
        rect = pygame.Rect(bx, y, button_width, height)
        pygame.draw.rect(screen, (36, 40, 46), rect, border_radius=max(2, round(4 * scale)))
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
        state.actions.append(InspectorAction(rect, "adjust_number", field=field, amount=step))
        bx += button_width + gap
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
        draw_clipped_text(screen, font, label, (224, 228, 232), rect, scale)
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
        draw_clipped_text(screen, font, issue.message, (238, 230, 214), rect, scale)
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
        elif obj_type in ("story", "storyImage"):
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


def region_cell_count(region: dict) -> int:
    total = 0
    for run in region.get("runs", []) or []:
        total += max(0, int(run.get("length", 0)))
    return total


def action_index(action: InspectorAction) -> int:
    if action.target is None:
        return -1
    return int(action.target[1])


def set_action_text_value(event: dict, encoded_field: str, value: str) -> bool:
    _prefix, raw_index, field = encoded_field.split(":", 2)
    actions = event.setdefault("actions", [])
    index = int(raw_index)
    if not (0 <= index < len(actions)):
        return False
    actions[index][field] = coerce_action_field_value(field, value)
    return True


def coerce_action_field_value(field: str, value: str) -> object:
    if field in ("x", "y", "level", "hp", "variant"):
        try:
            return int(value)
        except ValueError:
            return 0
    return value.strip()


def delete_action_at(event: dict, index: int, state: InspectorState) -> bool:
    actions = event.setdefault("actions", [])
    if 0 <= index < len(actions):
        del actions[index]
        state.clear_text()
        return True
    return False


def cycle_action_value(event: dict, index: int, field: str, values: tuple[str, ...], state: InspectorState) -> bool:
    action = action_at(event, index)
    if action is None:
        return False
    current = str(action.get(field, values[0]))
    try:
        next_index = (values.index(current) + 1) % len(values)
    except ValueError:
        next_index = 0
    action[field] = values[next_index]
    normalize_action_defaults(action)
    state.clear_text()
    return True


def adjust_action_int(event: dict, index: int, field: str, amount: int, state: InspectorState) -> bool:
    action = action_at(event, index)
    if action is None or not field:
        return False
    try:
        current = int(action.get(field, 0))
    except (TypeError, ValueError):
        current = 0
    action[field] = current + amount
    if field in ("level", "hp"):
        action[field] = max(1, action[field])
    state.clear_text()
    return True


def set_action_cell(event: dict, index: int, selected_cell: tuple[int, int] | None, state: InspectorState) -> bool:
    action = action_at(event, index)
    if action is None or selected_cell is None:
        return False
    action["x"] = int(selected_cell[0])
    action["y"] = int(selected_cell[1])
    state.clear_text()
    return True


def add_region_fallstone_actions(level: dict, event: dict, state: InspectorState) -> bool:
    region_id = str(event.get("region", "")).strip()
    if not region_id:
        return False
    region = next((item for item in level.get("regions", []) or [] if str(item.get("id", "")).strip() == region_id), None)
    if region is None:
        return False
    actions = event.setdefault("actions", [])
    existing = {(int(action.get("x", 0)), int(action.get("y", 0))) for action in actions if action.get("type") == "fallStone"}
    changed = False
    for x, y in sorted(region_cells(region), key=lambda cell: (cell[1], cell[0])):
        if (x, y) in existing:
            continue
        actions.append({"type": "fallStone", "x": x, "y": y})
        changed = True
    state.clear_text()
    return changed


def action_at(event: dict, index: int) -> dict | None:
    actions = event.setdefault("actions", [])
    if 0 <= index < len(actions) and isinstance(actions[index], dict):
        return actions[index]
    return None


def normalize_action_defaults(action: dict) -> None:
    action_type = action.get("type", "fallStone")
    if action_type == "showMonologue":
        action.setdefault("text", "")
        return

    action.setdefault("x", 0)
    action.setdefault("y", 0)
    if action_type == "setTile":
        action.setdefault("tile", "floor")
        action.setdefault("variant", -1)
    elif action_type == "spawnObject":
        action.setdefault("objectType", "plate")
    elif action_type == "spawnEnemy":
        action.setdefault("objectType", "patrol")
        action.setdefault("level", 3)
        action.setdefault("hp", 2)


def action_uses_cell(action: dict) -> bool:
    return action.get("type", "fallStone") not in ("showMonologue",)


def region_cells(region: dict) -> set[tuple[int, int]]:
    cells: set[tuple[int, int]] = set()
    for run in region.get("runs", []) or []:
        y = int(run.get("y", 0))
        x0 = int(run.get("x", 0))
        length = max(0, int(run.get("length", 0)))
        for x in range(x0, x0 + length):
            cells.add((x, y))
    return cells


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


def clamp_number_field(field: str, current: object, amount: float) -> int | float:
    if not isinstance(current, (int, float)):
        current = 0
    value = float(current) + amount
    if field == "scale":
        return round(max(0.05, min(8.0, value)), 3)
    if field == "sortingOrder":
        return int(round(max(-100, min(100, value))))
    if field == "intensity":
        return round(max(0.01, min(8.0, value)), 3)
    if field == "radius":
        return round(max(0.1, min(24.0, value)), 3)
    if field in ("outerAngle", "innerAngle"):
        return round(max(1.0, min(360.0, value)), 2)
    if field == "rotation":
        wrapped = value % 360.0
        return int(round(wrapped)) if abs(wrapped - round(wrapped)) < 0.0001 else round(wrapped, 2)
    return round(value, 3)


def format_number(value: object) -> str:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return str(value)
    if number.is_integer():
        return str(int(number))
    return f"{number:.3f}".rstrip("0").rstrip(".")


def signed_number(value: float) -> str:
    prefix = "+" if value > 0 else ""
    return prefix + format_number(value)


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
        return f"Enemy: {target.get('type', 'patrol')}"
    if kind == "player":
        return "Player start"
    if kind == "exit":
        return "Exit"
    if kind == "decoration":
        return f"Texture: {target.get('id', 'decoration')}"
    if kind == "light":
        return f"Light: {target.get('id', 'light')}"
    if kind == "event":
        return f"Event: {target.get('id', 'event')}"
    if kind == "region":
        return f"Region: {target.get('id', 'region')}"
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


def draw_field_value(
    screen: pygame.Surface,
    font: pygame.font.Font,
    value: str,
    rect: pygame.Rect,
    scale: float,
    active: bool = False,
    cursor_index: int | None = None,
) -> None:
    x = rect.x + round(8 * scale)
    y = rect.y + round(6 * scale)
    max_width = max(10, rect.width - round(16 * scale))
    max_lines = max(1, (rect.height - round(8 * scale)) // max(1, font.get_height()))
    wrapped = wrap_text_with_indices(value, font, max_width)
    lines = [line for line, _start in wrapped]
    cursor_index = max(0, min(cursor_index if cursor_index is not None else len(value), len(value)))
    caret_line_index = wrapped_line_index_for_cursor(wrapped, cursor_index) if active else 0
    start_line = max(0, caret_line_index - max_lines + 1) if active else 0
    previous_clip = screen.get_clip()
    screen.set_clip(rect)
    for line in lines[start_line:start_line + max_lines]:
        screen.blit(font.render(line, True, (230, 234, 238)), (x, y))
        y += font.get_height() + 2

    if active and pygame.time.get_ticks() % 1000 < 540:
        caret_local_line = caret_line_index - start_line
        if 0 <= caret_local_line < max_lines:
            caret_line, caret_start = wrapped[caret_line_index] if wrapped else ("", 0)
            caret_text = caret_line[:max(0, cursor_index - caret_start)]
            caret_x = min(rect.right - round(7 * scale), x + font.size(caret_text)[0] + 1)
            caret_y = rect.y + round(6 * scale) + caret_local_line * (font.get_height() + 2)
            pygame.draw.line(screen, (156, 230, 255), (caret_x, caret_y), (caret_x, caret_y + font.get_height()), max(1, round(2 * scale)))
    screen.set_clip(previous_clip)


def draw_clipped_text(
    screen: pygame.Surface,
    font: pygame.font.Font,
    text: object,
    color: tuple[int, int, int],
    rect: pygame.Rect,
    scale: float,
) -> None:
    previous_clip = screen.get_clip()
    screen.set_clip(rect)
    label = ellipsize(str(text), font, max(4, rect.width - round(14 * scale)))
    screen.blit(font.render(label, True, color), (rect.x + round(7 * scale), rect.y + max(2, (rect.height - font.get_height()) // 2)))
    screen.set_clip(previous_clip)


def ellipsize(text: str, font: pygame.font.Font, max_width: int) -> str:
    if font.size(text)[0] <= max_width:
        return text
    suffix = "..."
    available = max(0, max_width - font.size(suffix)[0])
    result = ""
    for char in text:
        if font.size(result + char)[0] > available:
            break
        result += char
    return result + suffix


def wrap_text(value: str, font: pygame.font.Font, max_width: int) -> list[str]:
    return [line for line, _start in wrap_text_with_indices(value, font, max_width)]


def wrap_text_with_indices(value: str, font: pygame.font.Font, max_width: int) -> list[tuple[str, int]]:
    source_lines = value.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    indexed: list[tuple[str, int]] = []
    base_index = 0
    for source in source_lines:
        if not source:
            indexed.append(("", base_index))
            base_index += 1
            continue
        current = ""
        current_start = base_index
        word_start = base_index
        for word in source.split(" "):
            candidate = word if not current else f"{current} {word}"
            if font.size(candidate)[0] <= max_width:
                current = candidate
                word_start += len(word) + 1
                continue
            if current:
                indexed.append((current, current_start))
                current_start = word_start
            current = fit_word_indexed(word, font, max_width, indexed, current_start)
            word_start += len(word) + 1
            current_start = word_start - len(current) - 1 if current else word_start
        indexed.append((current, current_start))
        base_index += len(source) + 1
    return indexed or [("", 0)]


def fit_word_indexed(word: str, font: pygame.font.Font, max_width: int, result: list[tuple[str, int]], start_index: int) -> str:
    if font.size(word)[0] <= max_width:
        return word

    current = ""
    current_start = start_index
    for offset, char in enumerate(word):
        candidate = current + char
        if current and font.size(candidate)[0] > max_width:
            result.append((current, current_start))
            current = char
            current_start = start_index + offset
        else:
            current = candidate
    return current


def wrapped_line_index_for_cursor(wrapped: list[tuple[str, int]], cursor_index: int) -> int:
    if not wrapped:
        return 0
    for index, (line, start) in enumerate(wrapped):
        end = start + len(line)
        next_start = wrapped[index + 1][1] if index + 1 < len(wrapped) else None
        if start <= cursor_index <= end:
            return index
        if next_start is not None and end < cursor_index < next_start:
            return index + 1
    return len(wrapped) - 1


def cursor_index_from_pos(
    value: str,
    rect: pygame.Rect,
    pos: tuple[int, int],
    font: pygame.font.Font,
    scale: float,
    visible_cursor_index: int | None = None,
) -> int:
    max_width = max(10, rect.width - round(16 * scale))
    wrapped = wrap_text_with_indices(value, font, max_width)
    max_lines = max(1, (rect.height - round(8 * scale)) // max(1, font.get_height()))
    line_height = font.get_height() + 2
    if visible_cursor_index is None:
        start_line = 0
    else:
        visible_cursor_index = max(0, min(visible_cursor_index, len(value)))
        caret_line_index = wrapped_line_index_for_cursor(wrapped, visible_cursor_index)
        start_line = max(0, caret_line_index - max_lines + 1)
    local_y = max(0, pos[1] - rect.y - round(6 * scale))
    line_index = max(0, min(len(wrapped) - 1, start_line + local_y // max(1, line_height)))
    line, start = wrapped[line_index]
    local_x = max(0, pos[0] - rect.x - round(8 * scale))
    offset = 0
    for index in range(len(line) + 1):
        if font.size(line[:index])[0] >= local_x:
            offset = index
            break
        offset = index
    return max(0, min(len(value), start + offset))


def line_boundary(text: str, cursor_index: int, direction: int) -> int:
    cursor_index = max(0, min(cursor_index, len(text)))
    if direction < 0:
        return text.rfind("\n", 0, cursor_index) + 1

    next_break = text.find("\n", cursor_index)
    return len(text) if next_break < 0 else next_break


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
            return min(99, max(1, int(value)))
        except ValueError:
            return 3
    if field == "hp":
        try:
            return max(1, int(value))
        except ValueError:
            return 2
    if field in ("x", "y", "scale", "rotation", "intensity", "radius", "outerAngle", "innerAngle", "hearing", "vision"):
        parsed = parse_number(value)
        return parsed if isinstance(parsed, (int, float)) else 0
    if field == "sortingOrder":
        try:
            return int(round(float(value)))
        except ValueError:
            return 0
    return value


def is_allowed_text(value: str, field: str) -> bool:
    if is_action_field(field, "text") or field in ("text", "actions"):
        return all(ch >= " " for ch in value)
    if field.startswith("action:"):
        return all(ch >= " " for ch in value)
    if field in ("requiresPlates", "requiresStories", "requiresEnemies"):
        return all(ch.isalnum() or ch in "_-., " for ch in value)
    if field in ("requiresStats", "conditions"):
        return all(ch.isalnum() or ch in "_-.,;()[] " for ch in value)
    if field == "level":
        return value.isdigit()
    if field == "hp":
        return value.isdigit()
    if field in ("x", "y", "scale", "rotation", "intensity", "radius", "outerAngle", "innerAngle", "hearing", "vision", "sortingOrder"):
        return value.isdigit() or value in ".-"
    if field in ("texturePath", "imagePath", "targetLevel"):
        return all(ch >= " " and ch not in "\"<>" for ch in value)
    if field == "color":
        return all(ch.isalnum() or ch in "#(),. " for ch in value)
    return all(ch.isalnum() or ch in "_-." for ch in value)


def is_multiline_field(field: str) -> bool:
    return field in ("text", "actions", "conditions", "requiresStats") or is_action_field(field, "text")


def is_action_field(field: str, action_field: str) -> bool:
    return field.startswith("action:") and field.endswith(f":{action_field}")


def coerce_action_value(field: str, value: object) -> object:
    if field in ("once", "castsShadow"):
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
