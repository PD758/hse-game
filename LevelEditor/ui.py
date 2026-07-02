from __future__ import annotations

import pygame

from palette import TOOLS


def sidebar_width(scale: float = 1.0) -> int:
    return round(240 * scale)


def tool_at(pos: tuple[int, int], scale: float = 1.0) -> int | None:
    width = sidebar_width(scale)
    if pos[0] < 0 or pos[0] > width:
        return None

    margin = round(12 * scale)
    item_height = round(28 * scale)
    item_step = round(34 * scale)
    y = round(52 * scale)
    for index, _tool in enumerate(TOOLS):
        rect = pygame.Rect(margin, y, width - margin * 2, item_height)
        if rect.collidepoint(pos):
            return index
        y += item_step
    return None


def save_button_rect(scale: float, screen_height: int) -> pygame.Rect:
    width = sidebar_width(scale)
    margin = round(12 * scale)
    return pygame.Rect(margin, screen_height - round(136 * scale), width - margin * 2, round(30 * scale))


def save_button_at(pos: tuple[int, int], scale: float, screen_height: int) -> bool:
    return save_button_rect(scale, screen_height).collidepoint(pos)


def draw_sidebar(screen: pygame.Surface, font: pygame.font.Font, selected: int, path: str, dirty: bool, patrol_mode: bool, scale: float = 1.0) -> None:
    width = sidebar_width(scale)
    margin = round(12 * scale)
    title_x = round(16 * scale)
    item_height = round(28 * scale)
    item_step = round(34 * scale)
    border_radius = max(2, round(4 * scale))
    pygame.draw.rect(screen, (24, 26, 30), pygame.Rect(0, 0, width, screen.get_height()))
    title = font.render("Level Editor", True, (230, 234, 238))
    screen.blit(title, (title_x, round(14 * scale)))

    y = round(52 * scale)
    for index, tool in enumerate(TOOLS):
        rect = pygame.Rect(margin, y, width - margin * 2, item_height)
        color = (58, 66, 74) if index == selected else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=border_radius)
        label = f"{tool['key']}  {tool['label']}"
        screen.blit(font.render(label, True, (224, 228, 232)), (round(22 * scale), y + round(6 * scale)))
        y += item_step

    save_rect = save_button_rect(scale, screen.get_height())
    pygame.draw.rect(screen, (48, 76, 86) if dirty else (36, 44, 48), save_rect, border_radius=border_radius)
    screen.blit(font.render("Save", True, (230, 234, 238)), (save_rect.x + round(10 * scale), save_rect.y + round(7 * scale)))

    status_y = screen.get_height() - round(96 * scale)
    status = "dirty" if dirty else "saved"
    if patrol_mode:
        status += " | patrol"
    if TOOLS[selected]["kind"] == "region":
        status += " | regions"
    screen.blit(font.render(status, True, (166, 212, 230)), (title_x, status_y))
    max_chars = max(18, int(34 * scale))
    clipped = path if len(path) < max_chars else "..." + path[-(max_chars - 3):]
    screen.blit(font.render(clipped, True, (160, 166, 172)), (title_x, status_y + round(28 * scale)))
