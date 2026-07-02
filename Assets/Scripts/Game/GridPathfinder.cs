using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class GridPathfinder
{
    private readonly int width;
    private readonly int height;
    private readonly Vector2Int[] parents;
    private readonly bool[] visited;
    private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();

    public GridPathfinder(int width, int height)
    {
        this.width = width;
        this.height = height;
        parents = new Vector2Int[width * height];
        visited = new bool[width * height];
    }

    public bool TryFindNextPathCell(Vector2Int start, Vector2Int goal, Func<Vector2Int, Vector2Int, bool> passable, out Vector2Int nextCell)
    {
        nextCell = start;
        if (!Inside(start) || !Inside(goal))
            return false;

        Array.Fill(visited, false);
        Array.Fill(parents, new Vector2Int(-1, -1));
        queue.Clear();

        int startIndex = CellIndex(start);
        visited[startIndex] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal)
                break;

            foreach (Vector2Int next in GoalOrderedCardinalCells(current, goal))
            {
                if (!Inside(next))
                    continue;

                int index = CellIndex(next);
                if (visited[index] || !passable(next, goal))
                    continue;

                visited[index] = true;
                parents[index] = current;
                queue.Enqueue(next);
            }
        }

        if (!visited[CellIndex(goal)])
            return false;

        Vector2Int step = goal;
        while (parents[CellIndex(step)] != start)
        {
            step = parents[CellIndex(step)];
            if (step.x < 0)
                return false;
        }

        nextCell = step;
        return true;
    }

    private IEnumerable<Vector2Int> GoalOrderedCardinalCells(Vector2Int cell, Vector2Int goal)
    {
        Vector2Int up = cell + Vector2Int.up;
        Vector2Int down = cell + Vector2Int.down;
        Vector2Int left = cell + Vector2Int.left;
        Vector2Int right = cell + Vector2Int.right;
        bool usedUp = false;
        bool usedDown = false;
        bool usedLeft = false;
        bool usedRight = false;

        for (int emitted = 0; emitted < 4; emitted++)
        {
            int bestSlot = -1;
            int bestScore = int.MaxValue;
            ConsiderPathNeighbor(up, 0, usedUp, goal, ref bestSlot, ref bestScore);
            ConsiderPathNeighbor(down, 1, usedDown, goal, ref bestSlot, ref bestScore);
            ConsiderPathNeighbor(left, 2, usedLeft, goal, ref bestSlot, ref bestScore);
            ConsiderPathNeighbor(right, 3, usedRight, goal, ref bestSlot, ref bestScore);

            switch (bestSlot)
            {
                case 0:
                    usedUp = true;
                    yield return up;
                    break;
                case 1:
                    usedDown = true;
                    yield return down;
                    break;
                case 2:
                    usedLeft = true;
                    yield return left;
                    break;
                default:
                    usedRight = true;
                    yield return right;
                    break;
            }
        }
    }

    private static void ConsiderPathNeighbor(Vector2Int cell, int slot, bool used, Vector2Int goal, ref int bestSlot, ref int bestScore)
    {
        if (used)
            return;

        int score = Mathf.Abs(cell.x - goal.x) + Mathf.Abs(cell.y - goal.y);
        if (score < bestScore)
        {
            bestSlot = slot;
            bestScore = score;
        }
    }

    private bool Inside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    private int CellIndex(Vector2Int cell)
    {
        return cell.y * width + cell.x;
    }
}
