from __future__ import annotations

DEFAULT_WIDTH = 39
DEFAULT_HEIGHT = 21

TILE_WALL = "wall"
TILE_FLOOR = "floor"
TILE_RUBBLE = "rubble"

TILE_TYPES = (TILE_WALL, TILE_FLOOR, TILE_RUBBLE)

OBJECT_TYPES = (
    "gate",
    "remote",
    "trap",
    "story",
    "heal",
    "plate",
    "stone",
)

ENEMY_TYPE = "enemy"


def default_level() -> dict:
    return {
        "version": 1,
        "id": "new_level",
        "size": {"width": DEFAULT_WIDTH, "height": DEFAULT_HEIGHT},
        "playerStart": {"x": 0, "y": 0},
        "exits": [],
        "tiles": [{"tile": TILE_FLOOR, "y": 0, "x": 0, "length": 1}],
        "walls": [],
        "objects": [],
        "enemies": [],
        "logic": {
            "gates": [],
            "branchTriggers": [],
            "branchBlocks": [],
            "exitRules": [],
        },
        "regions": [],
    }


def point(x: int, y: int) -> dict:
    return {"x": int(x), "y": int(y)}


def object_at(obj: dict) -> tuple[int, int]:
    return int(obj.get("x", 0)), int(obj.get("y", 0))
