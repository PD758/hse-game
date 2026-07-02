from __future__ import annotations

import pygame

from palette import TOOLS


TOOL_GROUP_LABELS = {
    "cursor": "Select",
    "region": "Logic",
    "tile": "Tiles",
    "object": "Objects",
    "enemy": "Actors",
    "exit": "Logic",
    "decoration": "Visuals",
    "light": "Visuals",
}


def sidebar_width(scale: float = 1.0) -> int:
    return round(240 * scale)


def top_bar_height(scale: float = 1.0) -> int:
    return round(36 * scale)


def tool_at(pos: tuple[int, int], scale: float = 1.0) -> int | None:
    width = sidebar_width(scale)
    if pos[0] < 0 or pos[0] > width:
        return None

    margin = round(12 * scale)
    item_height = round(28 * scale)
    y = round(52 * scale)
    current_group = ""
    for index, tool in enumerate(TOOLS):
        group = TOOL_GROUP_LABELS.get(tool["kind"], "Tools")
        if group != current_group:
            y += round(22 * scale)
            current_group = group
        rect = pygame.Rect(margin, y, width - margin * 2, item_height)
        if rect.collidepoint(pos):
            return index
        y += round(32 * scale)
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
    border_radius = max(2, round(4 * scale))
    pygame.draw.rect(screen, (24, 26, 30), pygame.Rect(0, 0, width, screen.get_height()))
    title = font.render("Level Editor", True, (230, 234, 238))
    screen.blit(title, (title_x, round(14 * scale)))

    y = round(52 * scale)
    current_group = ""
    for index, tool in enumerate(TOOLS):
        group = TOOL_GROUP_LABELS.get(tool["kind"], "Tools")
        if group != current_group:
            y += round(22 * scale)
            screen.blit(font.render(group.upper(), True, (126, 142, 150)), (title_x, y - round(18 * scale)))
            current_group = group
        rect = pygame.Rect(margin, y, width - margin * 2, item_height)
        color = (66, 84, 92) if index == selected else (36, 40, 46)
        pygame.draw.rect(screen, color, rect, border_radius=border_radius)
        if index == selected:
            pygame.draw.rect(screen, (118, 226, 255), rect, max(1, round(2 * scale)), border_radius=border_radius)
        label = f"{tool['key']}  {tool['label']}"
        screen.blit(font.render(label, True, (224, 228, 232)), (round(22 * scale), y + round(6 * scale)))
        y += round(32 * scale)

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


def draw_top_status(
    screen: pygame.Surface,
    font: pygame.font.Font,
    selected_tool: int,
    selected_ref: tuple[str, int] | None,
    hover_cell: tuple[int, int] | None,
    dirty: bool,
    validation_issues: list,
    zoom: int,
    scale: float = 1.0,
) -> None:
    left = sidebar_width(scale)
    height = top_bar_height(scale)
    rect = pygame.Rect(left, 0, screen.get_width() - left - round(320 * scale), height)
    pygame.draw.rect(screen, (18, 21, 25), rect)
    pygame.draw.line(screen, (48, 56, 62), rect.bottomleft, rect.bottomright)

    errors = sum(1 for issue in validation_issues if issue.severity == "error")
    warnings = sum(1 for issue in validation_issues if issue.severity == "warning")
    tool = TOOLS[selected_tool]
    selected = "none" if selected_ref is None else f"{selected_ref[0]}:{selected_ref[1]}"
    cell = "-" if hover_cell is None else f"{hover_cell[0]},{hover_cell[1]}"
    save_state = "dirty" if dirty else "saved"
    text = f"{save_state}    tool: {tool['label']}    selected: {selected}    cell: {cell}    zoom: {zoom}px    validation: {errors}E/{warnings}W"
    color = (238, 204, 128) if dirty else (178, 218, 198)
    screen.blit(font.render(text, True, color), (left + round(14 * scale), round(9 * scale)))
