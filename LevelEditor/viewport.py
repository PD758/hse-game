from __future__ import annotations

import math

import pygame


class Viewport:
    def __init__(self, width: int, height: int, cell_size: int = 32):
        self.grid_width = width
        self.grid_height = height
        self.cell_size = max(12, min(96, cell_size))
        self.offset = pygame.Vector2(260, 40)

    def zoom(self, delta: int, mouse_pos: tuple[int, int]) -> None:
        before = self.screen_to_cell_float(mouse_pos)
        self.cell_size = max(12, min(96, self.cell_size + delta * 4))
        self.offset.x = mouse_pos[0] - before.x * self.cell_size
        self.offset.y = mouse_pos[1] - (self.grid_height - 1 - before.y) * self.cell_size

    def pan(self, delta: tuple[int, int]) -> None:
        self.offset += pygame.Vector2(delta)

    def cell_to_screen(self, x: int, y: int) -> pygame.Rect:
        screen_x = self.offset.x + x * self.cell_size
        screen_y = self.offset.y + (self.grid_height - 1 - y) * self.cell_size
        return pygame.Rect(round(screen_x), round(screen_y), self.cell_size, self.cell_size)

    def screen_to_cell(self, pos: tuple[int, int]) -> tuple[int, int] | None:
        x = math.floor((pos[0] - self.offset.x) / self.cell_size)
        row_from_top = math.floor((pos[1] - self.offset.y) / self.cell_size)
        y = self.grid_height - 1 - row_from_top
        if 0 <= x < self.grid_width and 0 <= y < self.grid_height:
            return x, y
        return None

    def screen_to_cell_float(self, pos: tuple[int, int]) -> pygame.Vector2:
        x = (pos[0] - self.offset.x) / self.cell_size
        y = self.grid_height - 1 - ((pos[1] - self.offset.y) / self.cell_size)
        return pygame.Vector2(x, y)

    def draw_grid(self, screen: pygame.Surface) -> None:
        color = (74, 78, 84)
        for x in range(self.grid_width + 1):
            x_pos = self.offset.x + x * self.cell_size
            pygame.draw.line(screen, color, (x_pos, self.offset.y), (x_pos, self.offset.y + self.grid_height * self.cell_size), 1)
        for y in range(self.grid_height + 1):
            y_pos = self.offset.y + y * self.cell_size
            pygame.draw.line(screen, color, (self.offset.x, y_pos), (self.offset.x + self.grid_width * self.cell_size, y_pos), 1)
