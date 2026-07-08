using System.Collections.Generic;
using UnityEngine;

public sealed partial class PrototypeGame
{
    private const string RoomFogRootName = "Room Fog";
    private const int RoomFogSortingOrder = 30;
    private const float RoomFogFadeSpeed = 8.5f;
    private static readonly Color RoomFogColor = new Color(0.025f, 0.034f, 0.050f, 1f);
    private static readonly Vector2Int[] RoomFogCardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };
    private static readonly Vector2Int[] RoomFogBoundaryDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1),
    };

    private readonly bool[,] roomFogCurrentVisible = new bool[Width, Height];
    private readonly bool[,] roomFogExplored = new bool[Width, Height];
    private readonly GameObject[,] roomFogViews = new GameObject[Width, Height];
    private readonly float[,] roomFogCurrentAlpha = new float[Width, Height];
    private readonly float[,] roomFogTargetAlpha = new float[Width, Height];
    private Sprite roomFogSprite;
    private bool roomFogDirty = true;
    private Vector2Int lastRoomFogPlayerCell = new Vector2Int(-1, -1);

    private void CreateRoomFogViews(Transform parent)
    {
        GameObject existingRoot = FindSceneObjectIncludingInactive(RoomFogRootName);
        if (existingRoot != null)
            DestroyRuntimeObject(existingRoot);

        var root = new GameObject(RoomFogRootName);
        if (parent != null)
            root.transform.SetParent(parent);

        Sprite sprite = RoomFogSprite();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GameObject view = new GameObject($"Fog {x},{y}");
                view.transform.SetParent(root.transform);
                view.transform.position = ToWorld(cell);
                view.transform.localScale = Vector3.one;
                SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(RoomFogColor.r, RoomFogColor.g, RoomFogColor.b, 1f);
                renderer.sortingOrder = RoomFogSortingOrder;
                if (Urp2DLighting.SpriteUnlitMaterial != null)
                    renderer.sharedMaterial = Urp2DLighting.SpriteUnlitMaterial;
                roomFogViews[x, y] = view;
                roomFogCurrentAlpha[x, y] = 1f;
                roomFogTargetAlpha[x, y] = 1f;
            }
        }

        roomFogDirty = true;
        lastRoomFogPlayerCell = new Vector2Int(-1, -1);
    }

    private void BindRoomFogViews(Transform tileRoot)
    {
        Transform root = FindSceneObjectIncludingInactive(RoomFogRootName)?.transform;
        if (root == null)
        {
            CreateRoomFogViews(tileRoot);
            return;
        }

        if (tileRoot != null && root.parent != tileRoot)
            root.SetParent(tileRoot);

        Sprite sprite = RoomFogSprite();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                GameObject view = FindChildObject(root, $"Fog {x},{y}");
                if (view == null)
                {
                    view = new GameObject($"Fog {x},{y}");
                    view.transform.SetParent(root);
                }

                view.SetActive(true);
                view.transform.position = ToWorld(new Vector2Int(x, y));
                view.transform.localScale = Vector3.one;
                SpriteRenderer renderer = view.GetComponent<SpriteRenderer>() ?? view.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = RoomFogSortingOrder;
                renderer.color = new Color(RoomFogColor.r, RoomFogColor.g, RoomFogColor.b, roomFogCurrentAlpha[x, y]);
                if (Urp2DLighting.SpriteUnlitMaterial != null)
                    renderer.sharedMaterial = Urp2DLighting.SpriteUnlitMaterial;
                roomFogViews[x, y] = view;
            }
        }

        roomFogDirty = true;
        lastRoomFogPlayerCell = new Vector2Int(-1, -1);
    }

    private Sprite RoomFogSprite()
    {
        if (roomFogSprite != null)
            return roomFogSprite;

        Texture2D texture = whiteTexture != null ? whiteTexture : Texture2D.whiteTexture;
        roomFogSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
        roomFogSprite.name = "room_fog";
        return roomFogSprite;
    }

    private void ResetRoomFog()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                roomFogCurrentVisible[x, y] = false;
                roomFogExplored[x, y] = false;
                roomFogCurrentAlpha[x, y] = 1f;
                roomFogTargetAlpha[x, y] = 1f;
            }
        }

        roomFogDirty = true;
        lastRoomFogPlayerCell = new Vector2Int(-1, -1);
        RefreshRoomFog(true);
    }

    private void MarkRoomFogDirty()
    {
        roomFogDirty = true;
    }

    private void UpdateRoomFog(float dt)
    {
        if (!RoomFogReady())
            return;

        Vector2Int playerCell = PlayerCell();
        if (roomFogDirty || playerCell != lastRoomFogPlayerCell)
            RefreshRoomFog(false);

        float t = 1f - Mathf.Exp(-RoomFogFadeSpeed * Mathf.Max(0f, dt));
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float next = Mathf.Lerp(roomFogCurrentAlpha[x, y], roomFogTargetAlpha[x, y], t);
                if (Mathf.Abs(next - roomFogCurrentAlpha[x, y]) < 0.002f)
                    next = roomFogTargetAlpha[x, y];
                roomFogCurrentAlpha[x, y] = next;
                ApplyRoomFogAlpha(x, y, next);
            }
        }
    }

    private void RefreshRoomFog(bool immediate)
    {
        if (!RoomFogReady())
            return;

        RecalculateRoomFog();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (immediate)
                    roomFogCurrentAlpha[x, y] = roomFogTargetAlpha[x, y];
                ApplyRoomFogAlpha(x, y, roomFogCurrentAlpha[x, y]);
            }
        }
    }

    private void RecalculateRoomFog()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                roomFogCurrentVisible[x, y] = false;
        }

        Vector2Int playerCell = PlayerCell();
        FloodCurrentRoom(playerCell);
        RevealCurrentRoomBoundary();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (roomFogCurrentVisible[x, y])
                    roomFogExplored[x, y] = true;

                roomFogTargetAlpha[x, y] = FogAlphaForCell(x, y);
            }
        }

        lastRoomFogPlayerCell = playerCell;
        roomFogDirty = false;
    }

    private void FloodCurrentRoom(Vector2Int start)
    {
        if (!Inside(start) || !RoomFogCanEnter(start))
            return;

        var queue = new Queue<Vector2Int>();
        roomFogCurrentVisible[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int direction in RoomFogCardinalDirections)
            {
                Vector2Int next = cell + direction;
                if (!Inside(next) || roomFogCurrentVisible[next.x, next.y] || !RoomFogCanEnter(next))
                    continue;

                roomFogCurrentVisible[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }
    }

    private void RevealCurrentRoomBoundary()
    {
        var boundary = new List<Vector2Int>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!roomFogCurrentVisible[x, y] || !RoomFogCanEnter(new Vector2Int(x, y)))
                    continue;

                Vector2Int cell = new Vector2Int(x, y);
                foreach (Vector2Int direction in RoomFogBoundaryDirections)
                {
                    Vector2Int next = cell + direction;
                    if (Inside(next) && !RoomFogCanEnter(next))
                        boundary.Add(next);
                }
            }
        }

        foreach (Vector2Int cell in boundary)
            roomFogCurrentVisible[cell.x, cell.y] = true;
    }

    private bool RoomFogCanEnter(Vector2Int cell)
    {
        return Inside(cell) && !IsSolidCell(cell) && StoneAt(cell) == null;
    }

    private float FogAlphaForCell(int x, int y)
    {
        if (roomFogCurrentVisible[x, y])
            return 0f;
        if (roomFogExplored[x, y])
            return 0.55f;
        return 0.92f;
    }

    private bool RoomFogReady()
    {
        return roomFogViews[0, 0] != null;
    }

    private void ApplyRoomFogAlpha(int x, int y, float alpha)
    {
        GameObject view = roomFogViews[x, y];
        if (view == null)
            return;

        SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.color = new Color(RoomFogColor.r, RoomFogColor.g, RoomFogColor.b, alpha);
        view.SetActive(alpha > 0.01f);
    }
}
