from __future__ import annotations

import json
from pathlib import Path

from schema import TILE_FLOOR, TILE_RUBBLE, TILE_WALL, default_level


def load_level(path: Path) -> dict:
    if not path.exists():
        level = default_level()
        level["id"] = path.stem if path.name else "new_level"
        return level
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    return normalize_level(data)


def save_level(path: Path, level: dict, tiles: list[list[str]], tile_variants: list[list[int]] | None = None) -> None:
    out = dict(level)
    out["tiles"] = compact_tile_runs(tiles, tile_variants)
    out["walls"] = []
    out.pop("exit", None)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(out, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def normalize_level(data: dict) -> dict:
    base = default_level()
    base.update(data)
    base.setdefault("objects", [])
    base.setdefault("enemies", [])
    if not base.get("exits") and base.get("exit"):
        exit_cell = base.get("exit") or {}
        base["exits"] = [{
            "id": "exit_0",
            "x": int(exit_cell.get("x", 0)),
            "y": int(exit_cell.get("y", 0)),
            "branch": "none",
            "requiresGate": "",
            "targetLevel": "",
        }]
    base.setdefault("exits", [])
    base.setdefault("tiles", [])
    base.setdefault("walls", [])
    base.setdefault("logic", default_level()["logic"])
    base.setdefault("regions", [])
    return base


def expand_tiles(level: dict) -> list[list[str]]:
    size = level["size"]
    width = int(size["width"])
    height = int(size["height"])
    tiles = [[TILE_WALL for _ in range(height)] for _ in range(width)]

    for run in level.get("tiles", []):
        tile = run.get("tile", TILE_FLOOR)
        y = int(run.get("y", 0))
        x0 = int(run.get("x", 0))
        length = int(run.get("length", 0))
        for x in range(x0, x0 + length):
            if 0 <= x < width and 0 <= y < height:
                tiles[x][y] = tile

    for wall in level.get("walls", []):
        x = int(wall.get("x", 0))
        y = int(wall.get("y", 0))
        if 0 <= x < width and 0 <= y < height:
            tiles[x][y] = TILE_WALL

    return tiles


def expand_tile_variants(level: dict) -> list[list[int]]:
    size = level["size"]
    width = int(size["width"])
    height = int(size["height"])
    variants = [[-1 for _ in range(height)] for _ in range(width)]

    for run in level.get("tiles", []):
        variant = int(run.get("variant", -1))
        y = int(run.get("y", 0))
        x0 = int(run.get("x", 0))
        length = int(run.get("length", 0))
        for x in range(x0, x0 + length):
            if 0 <= x < width and 0 <= y < height:
                variants[x][y] = variant

    return variants


def compact_tile_runs(tiles: list[list[str]], tile_variants: list[list[int]] | None = None) -> list[dict]:
    width = len(tiles)
    height = len(tiles[0]) if width else 0
    runs: list[dict] = []
    for y in range(height):
        x = 0
        while x < width:
            tile = tiles[x][y]
            variant = variant_at(tile_variants, x, y)
            if tile == TILE_WALL and variant < 0:
                x += 1
                continue
            start = x
            while x < width and tiles[x][y] == tile and variant_at(tile_variants, x, y) == variant:
                x += 1
            run = {"tile": tile, "y": y, "x": start, "length": x - start}
            if variant >= 0:
                run["variant"] = variant
            runs.append(run)
    return runs


def variant_at(tile_variants: list[list[int]] | None, x: int, y: int) -> int:
    if tile_variants is None:
        return -1
    if x < 0 or x >= len(tile_variants) or not tile_variants:
        return -1
    column = tile_variants[x]
    if y < 0 or y >= len(column):
        return -1
    return int(column[y])
