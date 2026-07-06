from __future__ import annotations

TOOLS = [
    {"kind": "cursor", "value": "cursor", "label": "Cursor", "key": "c"},
    {"kind": "region", "value": "region", "label": "Regions", "key": "g"},
    {"kind": "tile", "value": "floor", "label": "Floor", "key": "1"},
    {"kind": "tile", "value": "wall", "label": "Wall", "key": "2"},
    {"kind": "tile", "value": "rubble", "label": "Rubble", "key": "3"},
    {"kind": "object", "value": "gate", "label": "Gate", "key": "4"},
    {"kind": "object", "value": "plate", "label": "Plate", "key": "5"},
    {"kind": "object", "value": "stone", "label": "Stone", "key": "6"},
    {"kind": "object", "value": "trap", "label": "Camera", "key": "7"},
    {"kind": "object", "value": "remote", "label": "Remote", "key": "8"},
    {"kind": "object", "value": "flashlight", "label": "Flashlight", "key": "f"},
    {"kind": "object", "value": "heal", "label": "Heal", "key": "9"},
    {"kind": "enemy", "value": "patrol", "label": "Enemy", "key": "0"},
    {"kind": "exit", "value": "exit", "label": "Exit", "key": "x"},
    {"kind": "object", "value": "story", "label": "Text Note", "key": "-"},
    {"kind": "object", "value": "storyImage", "label": "Image Note", "key": "="},
    {"kind": "decoration", "value": "decoration", "label": "Texture", "key": "t"},
    {"kind": "light", "value": "light", "label": "Light", "key": "i"},
]


def tool_by_key(key: str) -> int | None:
    for index, tool in enumerate(TOOLS):
        if tool["key"] == key:
            return index
    return None
