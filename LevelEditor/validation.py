from __future__ import annotations

from collections import deque
from dataclasses import dataclass
import json
from pathlib import Path

from schema import ENEMY_TYPE, OBJECT_TYPES, TILE_TYPES, TILE_WALL, object_at

STAT_OPERATORS = {"gt", "lt", "ge", "le", "eq", "ne"}
STAT_NAMES = {"enemiesKilled", "enemiesKilledOnLevel", "camerasBroken", "currentRating"}
EVENT_TRIGGERS = {"levelStart", "enterRegion", "statsChanged", "enemyKilled", "enemyGroupCleared"}
EVENT_ACTIONS = {"spawnEnemy", "spawnEnemies", "fallStone", "setTile", "spawnObject", "removeObject", "playEffect"}


@dataclass
class ValidationIssue:
    severity: str
    message: str
    cell: tuple[int, int] | None = None
    target: tuple[str, int] | None = None


def validate_level(level: dict, tiles: list[list[str]]) -> list[ValidationIssue]:
    issues: list[ValidationIssue] = []
    width = len(tiles)
    height = len(tiles[0]) if width else 0
    occupied: dict[tuple[int, int], list[tuple[str, int]]] = {}

    def add_occupied(cell: tuple[int, int], target: tuple[str, int]) -> None:
        occupied.setdefault(cell, []).append(target)

    player = level.get("playerStart", {})
    player_cell = (int(player.get("x", -1)), int(player.get("y", -1)))
    add_occupied(player_cell, ("player", 0))
    validate_walkable_cell(issues, tiles, player_cell, ("player", 0), "player start")

    plate_groups = set()
    story_ids = set()
    gate_ids = set()
    enemy_ids = set()
    enemy_groups = set()
    region_ids = set()
    visual_ids = set()
    existing_levels = collect_level_ids()

    exit_ids = set()
    exits = level.get("exits", []) or []
    if not exits:
        issues.append(ValidationIssue("warning", "level has no exits"))
    for index, exit_cell in enumerate(exits):
        target = ("exit", index)
        cell = (int(exit_cell.get("x", -1)), int(exit_cell.get("y", -1)))
        add_occupied(cell, target)
        validate_walkable_cell(issues, tiles, cell, target, "exit")

        exit_id = exit_cell.get("id", "")
        if exit_id in exit_ids:
            issues.append(ValidationIssue("error", f"duplicate exit id '{exit_id}'", cell, target))
        elif exit_id:
            exit_ids.add(exit_id)
        else:
            issues.append(ValidationIssue("warning", "exit has no id", cell, target))

        target_level = normalize_level_id(exit_cell.get("targetLevel", ""))
        if target_level and target_level not in existing_levels:
            issues.append(ValidationIssue("warning", f"exit target '{target_level}' was not found", cell, target))

        if not reachable(tiles, player_cell, cell):
            issues.append(ValidationIssue("warning", "exit is not reachable from player start", cell, target))
        if exit_cell.get("branch", "none") not in ("", "none"):
            issues.append(ValidationIssue("warning", "exit branch is ignored; use requiresGate with gate conditions instead", cell, target))

    for index, obj in enumerate(level.get("objects", [])):
        target = ("object", index)
        cell = object_at(obj)
        add_occupied(cell, target)
        validate_walkable_cell(issues, tiles, cell, target, obj.get("type", "object"))

        obj_type = obj.get("type", "")
        if obj_type == "plate":
            group = obj.get("group", "")
            if group:
                plate_groups.add(group)
            else:
                issues.append(ValidationIssue("warning", "plate has no group", cell, target))
        elif obj_type == "gate":
            gate_id = obj.get("id", "")
            if gate_id:
                gate_ids.add(gate_id)
            else:
                issues.append(ValidationIssue("error", "gate has no id", cell, target))
        elif obj_type == "story":
            story_id = obj.get("id", "")
            if story_id:
                story_ids.add(story_id)
            else:
                issues.append(ValidationIssue("error", "story has no id", cell, target))
            if not obj.get("text"):
                issues.append(ValidationIssue("warning", "story has empty text", cell, target))

    for index, obj in enumerate(level.get("objects", [])):
        if obj.get("type") != "gate":
            continue
        target = ("object", index)
        cell = object_at(obj)
        for group in obj.get("requiresPlates", []) or []:
            if group not in plate_groups:
                issues.append(ValidationIssue("error", f"gate requires missing plate group '{group}'", cell, target))
        for story_id in obj.get("requiresStories", []) or []:
            if story_id not in story_ids:
                issues.append(ValidationIssue("error", f"gate requires missing story '{story_id}'", cell, target))

    for index, exit_cell in enumerate(exits):
        requires_gate = exit_cell.get("requiresGate", "")
        if requires_gate and requires_gate not in gate_ids:
            cell = (int(exit_cell.get("x", -1)), int(exit_cell.get("y", -1)))
            issues.append(ValidationIssue("error", f"exit requires missing gate '{requires_gate}'", cell, ("exit", index)))

    for index, enemy in enumerate(level.get("enemies", [])):
        target = (ENEMY_TYPE, index)
        cell = (int(enemy.get("x", 0)), int(enemy.get("y", 0)))
        add_occupied(cell, target)
        validate_walkable_cell(issues, tiles, cell, target, "enemy")

        enemy_id = str(enemy.get("id", "")).strip()
        if not enemy_id:
            issues.append(ValidationIssue("error", "enemy has no id", cell, target))
        elif enemy_id in enemy_ids:
            issues.append(ValidationIssue("error", f"duplicate enemy id '{enemy_id}'", cell, target))
        else:
            enemy_ids.add(enemy_id)
        enemy_group = str(enemy.get("group", "")).strip()
        if enemy_group:
            enemy_groups.add(enemy_group)

        try:
            enemy_level = int(enemy.get("level", 3))
        except (TypeError, ValueError):
            enemy_level = 0
        if enemy_level < 1 or enemy_level > 9:
            issues.append(ValidationIssue("error", "enemy level must be 1..9", cell, target))

        try:
            hp = int(enemy.get("hp", 2))
        except (TypeError, ValueError):
            hp = 0
        if hp < 1:
            issues.append(ValidationIssue("error", "enemy hp must be >= 1", cell, target))

        patrol = enemy.get("patrol", [])
        if not patrol:
            issues.append(ValidationIssue("warning", "enemy has no patrol", cell, target))
        previous_patrol_cell = cell
        for point in patrol:
            if len(point) < 2:
                issues.append(ValidationIssue("error", "enemy patrol point is malformed", cell, target))
                continue
            patrol_cell = (int(point[0]), int(point[1]))
            validate_walkable_cell(issues, tiles, patrol_cell, target, "enemy patrol point")
            if not reachable(tiles, previous_patrol_cell, patrol_cell):
                issues.append(ValidationIssue("warning", "enemy patrol segment is not reachable", patrol_cell, target))
            previous_patrol_cell = patrol_cell

    for region in level.get("regions", []) or []:
        region_id = str(region.get("id", "")).strip()
        if region_id:
            region_ids.add(region_id)
        else:
            issues.append(ValidationIssue("error", "region has no id"))

    for index, obj in enumerate(level.get("objects", [])):
        if obj.get("type") != "gate":
            continue
        target = ("object", index)
        cell = object_at(obj)
        for enemy_id in obj.get("requiresEnemies", []) or []:
            if enemy_id not in enemy_ids:
                issues.append(ValidationIssue("error", f"gate requires missing enemy '{enemy_id}'", cell, target))
        for condition in obj.get("requiresStats", []) or []:
            validate_stat_condition(issues, condition, cell, target)

    event_ids = set()
    spawned_enemy_ids = set()
    spawned_enemy_groups = set()
    for index, event in enumerate(level.get("events", []) or []):
        validate_event(issues, event, index, tiles, event_ids, enemy_ids | spawned_enemy_ids, enemy_groups | spawned_enemy_groups, region_ids, spawned_enemy_ids, spawned_enemy_groups)

    for index, decoration in enumerate(level.get("decorations", []) or []):
        validate_decoration(issues, decoration, index, visual_ids)

    for index, light in enumerate(level.get("lights", []) or []):
        validate_light(issues, light, index, visual_ids)

    for cell, targets in occupied.items():
        if len(targets) > 1:
            issues.append(ValidationIssue("error", "multiple entities in one cell", cell, targets[0]))

    logic = level.get("logic", {})
    if logic.get("gates"):
        issues.append(ValidationIssue("warning", "logic.gates/openWhen is metadata only; configure gate object requirements instead"))
    if logic.get("exitRules"):
        issues.append(ValidationIssue("warning", "logic.exitRules is ignored; move exit gating to exits[].requiresGate"))
    for key in ("branchTriggers", "branchBlocks"):
        entries = logic.get(key, []) or []
        if entries:
            issues.append(ValidationIssue("warning", f"logic.{key} is ignored; use explicit walls/gates and gate requirements instead"))
        for index, area in enumerate(entries):
            if not area.get("area"):
                issues.append(ValidationIssue("error", f"{key}[{index}] has no area"))

    return issues


def validate_decoration(issues: list[ValidationIssue], decoration: dict, index: int, visual_ids: set[str]) -> None:
    target = ("decoration", index)
    visual_id = str(decoration.get("id", "")).strip()
    if not visual_id:
        issues.append(ValidationIssue("warning", "texture has no id", target=target))
    elif visual_id in visual_ids:
        issues.append(ValidationIssue("error", f"duplicate visual id '{visual_id}'", target=target))
    else:
        visual_ids.add(visual_id)

    texture_path = str(decoration.get("texturePath", "")).strip()
    validate_texture_path(issues, texture_path, target)
    scale = numeric_value(decoration.get("scale", 1.0), 1.0)
    if scale <= 0:
        issues.append(ValidationIssue("error", "texture scale must be > 0", target=target))
    elif scale > 8:
        issues.append(ValidationIssue("warning", "texture scale is very large", target=target))


def validate_light(issues: list[ValidationIssue], light: dict, index: int, visual_ids: set[str]) -> None:
    target = ("light", index)
    visual_id = str(light.get("id", "")).strip()
    if not visual_id:
        issues.append(ValidationIssue("warning", "light has no id", target=target))
    elif visual_id in visual_ids:
        issues.append(ValidationIssue("error", f"duplicate visual id '{visual_id}'", target=target))
    else:
        visual_ids.add(visual_id)

    light_type = str(light.get("type", "point")).strip()
    if light_type not in {"point", "cone"}:
        issues.append(ValidationIssue("error", f"unknown light type '{light_type}'", target=target))
    if numeric_value(light.get("intensity", 0), 0) <= 0:
        issues.append(ValidationIssue("error", "light intensity must be > 0", target=target))
    if numeric_value(light.get("radius", 0), 0) <= 0:
        issues.append(ValidationIssue("error", "light radius must be > 0", target=target))
    if not valid_color(str(light.get("color", ""))):
        issues.append(ValidationIssue("error", "light color must be #RRGGBB", target=target))
    if light_type == "cone":
        outer = numeric_value(light.get("outerAngle", 0), 0)
        inner = numeric_value(light.get("innerAngle", 0), 0)
        if not 1 <= outer <= 360:
            issues.append(ValidationIssue("error", "cone outerAngle must be 1..360", target=target))
        if not 0 <= inner <= outer:
            issues.append(ValidationIssue("warning", "cone innerAngle should be <= outerAngle", target=target))


def validate_texture_path(issues: list[ValidationIssue], texture_path: str, target: tuple[str, int]) -> None:
    if not texture_path:
        issues.append(ValidationIssue("error", "texturePath is required", target=target))
        return
    path = Path(texture_path)
    if path.is_absolute():
        issues.append(ValidationIssue("warning", "absolute texturePath is not portable", target=target))
        resolved = path
    else:
        normalized = texture_path.replace("\\", "/")
        if not normalized.startswith("Assets/Resources/"):
            issues.append(ValidationIssue("warning", "texturePath should be under Assets/Resources for builds", target=target))
        resolved = Path(__file__).resolve().parents[1] / normalized
    if not resolved.exists():
        issues.append(ValidationIssue("error", f"texture file not found: {texture_path}", target=target))


def numeric_value(value: object, fallback: float) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def valid_color(value: str) -> bool:
    text = value.strip()
    if not (text.startswith("#") and len(text) == 7):
        return False
    try:
        int(text[1:], 16)
    except ValueError:
        return False
    return True


def collect_level_ids() -> set[str]:
    levels_dir = Path(__file__).resolve().parents[1] / "Assets" / "Levels"
    result: set[str] = set()
    for path in levels_dir.glob("*.json"):
        result.add(normalize_level_id(path.name))
        try:
            with path.open("r", encoding="utf-8") as handle:
                data = json.load(handle)
            level_id = normalize_level_id(data.get("id", ""))
            if level_id:
                result.add(level_id)
        except (OSError, json.JSONDecodeError):
            continue
    return result


def normalize_level_id(value: object) -> str:
    text = "" if value is None else str(value).strip().replace("\\", "/")
    if not text:
        return ""
    text = text.rsplit("/", 1)[-1]
    if text.lower().endswith(".json"):
        text = text[:-5]
    return text


def validate_stat_condition(issues: list[ValidationIssue], condition: object, cell: tuple[int, int], target: tuple[str, int]) -> None:
    if not isinstance(condition, list) or len(condition) < 3:
        issues.append(ValidationIssue("error", "gate stat condition must be [op, stat, targetValue]", cell, target))
        return

    op = str(condition[0]).strip()
    stat_name = str(condition[1]).strip()
    if op not in STAT_OPERATORS:
        issues.append(ValidationIssue("error", f"unknown stat operator '{op}'", cell, target))
    if stat_name not in STAT_NAMES:
        issues.append(ValidationIssue("error", f"unknown gate stat '{stat_name}'", cell, target))

    try:
        target_value = float(condition[2])
    except (TypeError, ValueError):
        issues.append(ValidationIssue("error", "gate stat targetValue must be numeric", cell, target))
        return

    if stat_name == "currentRating" and not 0 <= target_value <= 100:
        issues.append(ValidationIssue("warning", "currentRating condition target should be 0..100", cell, target))


def validate_event(
    issues: list[ValidationIssue],
    event: dict,
    index: int,
    tiles: list[list[str]],
    event_ids: set[str],
    known_enemy_ids: set[str],
    known_enemy_groups: set[str],
    region_ids: set[str],
    spawned_enemy_ids: set[str],
    spawned_enemy_groups: set[str],
) -> None:
    target = ("event", index)
    event_id = str(event.get("id", "")).strip()
    if event_id in event_ids:
        issues.append(ValidationIssue("error", f"duplicate event id '{event_id}'", target=target))
    elif event_id:
        event_ids.add(event_id)
    else:
        issues.append(ValidationIssue("warning", "event has no id", target=target))

    trigger = str(event.get("trigger", "")).strip()
    if trigger not in EVENT_TRIGGERS:
        issues.append(ValidationIssue("error", f"unknown event trigger '{trigger}'", target=target))
    if trigger == "enterRegion" and event.get("region", "") not in region_ids:
        issues.append(ValidationIssue("error", f"event region '{event.get('region', '')}' was not found", target=target))
    if trigger == "enemyKilled" and event.get("enemyId", "") not in known_enemy_ids:
        issues.append(ValidationIssue("warning", f"event enemyId '{event.get('enemyId', '')}' is not present before this event", target=target))
    if trigger == "enemyGroupCleared" and event.get("enemyGroup", "") not in known_enemy_groups:
        issues.append(ValidationIssue("warning", f"event enemyGroup '{event.get('enemyGroup', '')}' is not present before this event", target=target))

    for condition in event.get("conditions", []) or []:
        validate_stat_condition(issues, condition, None, target)

    for action in event.get("actions", []) or []:
        validate_event_action(issues, action, target, tiles, known_enemy_ids, spawned_enemy_ids, spawned_enemy_groups)


def validate_event_action(
    issues: list[ValidationIssue],
    action: dict,
    target: tuple[str, int],
    tiles: list[list[str]],
    known_enemy_ids: set[str],
    spawned_enemy_ids: set[str],
    spawned_enemy_groups: set[str],
) -> None:
    action_type = str(action.get("type", "")).strip()
    if action_type not in EVENT_ACTIONS:
        issues.append(ValidationIssue("error", f"unknown event action '{action_type}'", target=target))
        return

    if action_type in {"fallStone", "setTile", "spawnObject", "removeObject", "playEffect", "spawnEnemy"}:
        cell = (int(action.get("x", 0)), int(action.get("y", 0)))
        if not inside(tiles, cell):
            issues.append(ValidationIssue("error", f"event action '{action_type}' target is outside level", cell, target))

    if action_type == "setTile" and action.get("tile", "") not in TILE_TYPES:
        issues.append(ValidationIssue("error", f"setTile uses unknown tile '{action.get('tile', '')}'", target=target))
    if action_type == "spawnObject" and action.get("objectType", action.get("type", "")) not in OBJECT_TYPES:
        issues.append(ValidationIssue("error", f"spawnObject uses unknown object type '{action.get('objectType', '')}'", target=target))

    enemies = []
    if action_type == "spawnEnemy":
        enemies = [action.get("enemy") or action]
    elif action_type == "spawnEnemies":
        enemies = action.get("enemies", []) or []
    for enemy in enemies:
        validate_spawned_enemy(issues, enemy, target, tiles, known_enemy_ids, spawned_enemy_ids, spawned_enemy_groups)


def validate_spawned_enemy(
    issues: list[ValidationIssue],
    enemy: dict,
    target: tuple[str, int],
    tiles: list[list[str]],
    known_enemy_ids: set[str],
    spawned_enemy_ids: set[str],
    spawned_enemy_groups: set[str],
) -> None:
    if not isinstance(enemy, dict):
        issues.append(ValidationIssue("error", "spawned enemy must be an object", target=target))
        return
    cell = (int(enemy.get("x", 0)), int(enemy.get("y", 0)))
    validate_walkable_cell(issues, tiles, cell, target, "spawned enemy")
    enemy_id = str(enemy.get("id", "")).strip()
    if not enemy_id:
        issues.append(ValidationIssue("error", "spawned enemy has no id", cell, target))
    elif enemy_id in known_enemy_ids or enemy_id in spawned_enemy_ids:
        issues.append(ValidationIssue("error", f"duplicate spawned enemy id '{enemy_id}'", cell, target))
    else:
        spawned_enemy_ids.add(enemy_id)
    enemy_group = str(enemy.get("group", "")).strip()
    if enemy_group:
        spawned_enemy_groups.add(enemy_group)


def validate_walkable_cell(issues: list[ValidationIssue], tiles: list[list[str]], cell: tuple[int, int], target: tuple[str, int], label: str) -> None:
    x, y = cell
    if not inside(tiles, cell):
        issues.append(ValidationIssue("error", f"{label} is outside level", cell, target))
    elif tiles[x][y] == TILE_WALL:
        issues.append(ValidationIssue("error", f"{label} is on wall", cell, target))


def reachable(tiles: list[list[str]], start: tuple[int, int], goal: tuple[int, int]) -> bool:
    if not inside(tiles, start) or not inside(tiles, goal):
        return False
    if tiles[start[0]][start[1]] == TILE_WALL or tiles[goal[0]][goal[1]] == TILE_WALL:
        return False

    queue = deque([start])
    visited = {start}
    while queue:
        cell = queue.popleft()
        if cell == goal:
            return True
        x, y = cell
        for next_cell in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if next_cell in visited or not inside(tiles, next_cell):
                continue
            if tiles[next_cell[0]][next_cell[1]] == TILE_WALL:
                continue
            visited.add(next_cell)
            queue.append(next_cell)
    return False


def inside(tiles: list[list[str]], cell: tuple[int, int]) -> bool:
    x, y = cell
    return 0 <= x < len(tiles) and len(tiles) > 0 and 0 <= y < len(tiles[0])
