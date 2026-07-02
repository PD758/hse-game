from __future__ import annotations

from pathlib import Path

import pygame


SPRITE_NAMES = {
    "floor": "floor_base.png",
    "rubble": "rubble.png",
    "wall": "wall_horizontal.png",
    "gate": "signal_gate_closed.png",
    "plate": "pressure_plate.png",
    "stone": "signal_blocker.png",
    "trap": "camera_trap.png",
    "remote": "remote.png",
    "heal": "heal_cassette.png",
    "story": "story_note.png",
    "exit": "tv_exit.png",
    "player": "player_base.png",
    "enemy": "enemy_patrol.png",
}

VARIANT_FILES = {
    "heal": ("heal_cassette.png",),
}

FALLBACK_COLORS = {
    "floor": (42, 46, 52),
    "rubble": (68, 61, 58),
    "wall": (22, 24, 28),
    "gate": (82, 108, 122),
    "plate": (88, 106, 108),
    "stone": (104, 104, 98),
    "trap": (98, 126, 146),
    "remote": (132, 48, 46),
    "heal": (86, 174, 122),
    "story": (168, 160, 124),
    "exit": (118, 210, 232),
    "player": (218, 226, 232),
    "enemy": (232, 88, 92),
}


class SpriteBank:
    def __init__(self, repo_root: Path):
        self.repo_root = repo_root
        self.source_dir = repo_root / "Assets" / "Generated" / "Prototype" / "Sprites"
        self._cache: dict[tuple[str, int], pygame.Surface] = {}
        self._path_cache: dict[tuple[str, int, int], pygame.Surface] = {}

    def get(self, name: str, size: int, variant: int = -1) -> pygame.Surface:
        key = (name, size, variant)
        if key in self._cache:
            return self._cache[key]

        surface = self._load(name, variant)
        scaled = pygame.transform.scale(surface, (size, size))
        self._cache[key] = scaled
        return scaled

    def variant_count(self, name: str) -> int:
        if name == "floor":
            return self._floor_variant_count()

        if name in VARIANT_FILES:
            return sum(1 for filename in VARIANT_FILES[name] if (self.source_dir / filename).exists())

        count = 0
        while (self.source_dir / f"{name}_{count}.png").exists():
            count += 1
        return count

    def get_path(self, texture_path: str, width: int, height: int) -> pygame.Surface:
        width = max(1, int(width))
        height = max(1, int(height))
        key = (texture_path, width, height)
        if key in self._path_cache:
            return self._path_cache[key]

        path = self.resolve_repo_path(texture_path)
        if path is not None and path.exists() and path.is_file():
            try:
                surface = self._load_image(path)
            except pygame.error:
                surface = self._missing_surface()
        else:
            surface = self._missing_surface()

        scaled = pygame.transform.scale(surface, (width, height))
        self._path_cache[key] = scaled
        return scaled

    def resolve_repo_path(self, texture_path: str) -> Path | None:
        raw = (texture_path or "").strip().replace("\\", "/")
        if not raw:
            return None
        path = Path(raw)
        if path.is_absolute():
            return path
        return (self.repo_root / raw).resolve()

    def _load(self, name: str, variant: int) -> pygame.Surface:
        if name == "floor" and variant >= 0:
            return self._load_floor_variant(variant)

        if variant >= 0:
            filenames = VARIANT_FILES.get(name)
            if filenames is not None and variant < len(filenames):
                variant_path = self.source_dir / filenames[variant]
                if variant_path.exists():
                    return self._load_image(variant_path)

            variant_path = self.source_dir / f"{name}_{variant}.png"
            if variant_path.exists():
                return self._load_image(variant_path)

        filename = SPRITE_NAMES.get(name)
        if filename:
            path = self.source_dir / filename
            if path.exists():
                return self._load_image(path)

        color = FALLBACK_COLORS.get(name, (255, 0, 255))
        surface = pygame.Surface((32, 32), pygame.SRCALPHA)
        surface.fill(color)
        return surface

    def _floor_variant_count(self) -> int:
        base = self.source_dir / SPRITE_NAMES["floor"]
        if not base.exists():
            return 0

        count = 1
        while (self.source_dir / f"floor_decal_{count - 1}.png").exists():
            count += 1
        return count

    def _load_floor_variant(self, variant: int) -> pygame.Surface:
        base_path = self.source_dir / SPRITE_NAMES["floor"]
        if base_path.exists():
            base = self._load_image(base_path).copy()
        else:
            base = pygame.Surface((32, 32), pygame.SRCALPHA)
            base.fill(FALLBACK_COLORS["floor"])

        if variant <= 0:
            return base

        decal_path = self.source_dir / f"floor_decal_{variant - 1}.png"
        if decal_path.exists():
            decal = self._load_image(decal_path)
            if decal.get_size() != base.get_size():
                decal = pygame.transform.scale(decal, base.get_size())
            base.blit(decal, (0, 0))
        return base

    @staticmethod
    def _load_image(path: Path) -> pygame.Surface:
        surface = pygame.image.load(str(path))
        if pygame.display.get_surface() is None:
            return surface
        return surface.convert_alpha()

    @staticmethod
    def _missing_surface() -> pygame.Surface:
        surface = pygame.Surface((32, 32), pygame.SRCALPHA)
        surface.fill((218, 0, 218, 210))
        pygame.draw.line(surface, (40, 20, 40), (0, 0), (31, 31), 3)
        pygame.draw.line(surface, (40, 20, 40), (31, 0), (0, 31), 3)
        return surface
