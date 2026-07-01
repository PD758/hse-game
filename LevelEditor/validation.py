from __future__ import annotations

from collections import deque
from dataclasses import dataclass
import json
from pathlib import Path

from schema import ENEMY_TYPE, TILE_WALL, object_at

STAT_OPERATORS = {"gt", "lt", "ge", "le", "eq", "ne"}
STAT_NAMES = {"enemiesKilled", "enemiesKilledOnLevel", "camerasBroken", "currentRating"}


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
