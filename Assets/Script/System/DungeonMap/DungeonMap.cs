using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoomCell
{
    public byte doors;        // NESW ��Ʈ ����ũ (Dir.N/E/S/W)
    public bool visited;      // ������ �� ��
    public bool discovered;   // �̴ϸʿ� �巯��(�ֺ� ���� ����)
    public RoomTag tag;       // Ÿ��(����/���� �� Ȯ���)
}

public enum RoomTag { Normal = 0, Boss = 1, Treasure = 2, Start = 3, Exit = 4 }

[Serializable]
public class DungeonMap
{
    public int width;
    public int height;
    public Vector2Int current;           // ���� �� ��ǥ
    public RoomCell[] cells;             // width*height

    public RoomCell this[int x, int y]
    {
        get => cells[y * width + x];
        set => cells[y * width + x] = value;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    /// <summary>
    /// �̷� ����. �⺻ �������� "�Ʒ��� �߾�"(y=0, x=w/2).
    /// </summary>
    public static DungeonMap Generate(int w, int h, int seed, int extraLinks = 0, Vector2Int? startOverride = null)
    {
        var map = new DungeonMap
        {
            width = Mathf.Max(2, w),
            height = Mathf.Max(2, h),
            cells = new RoomCell[w * h]
        };
        for (int i = 0; i < map.cells.Length; i++) map.cells[i] = new RoomCell();

        var rnd = (seed == 0) ? new System.Random() : new System.Random(seed);

        // �⺻ ����: "�Ʒ��� �߾�" (y=0)
        var start = startOverride ?? new Vector2Int(w / 2, 0);
        start.x = Mathf.Clamp(start.x, 0, w - 1);
        start.y = Mathf.Clamp(start.y, 0, h - 1);
        map.current = start;

        // --- DFS ��Ʈ��Ŀ�� ���� ���� �̷� ���� ---
        var stack = new Stack<Vector2Int>();
        var visited = new bool[w, h];
        stack.Push(start);
        visited[start.x, start.y] = true;

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            var dirs = new List<byte> { Dir.N, Dir.E, Dir.S, Dir.W };

            // ���� ����
            for (int i = 0; i < 8; i++)
            {
                int a = rnd.Next(dirs.Count);
                int b = rnd.Next(dirs.Count);
                (dirs[a], dirs[b]) = (dirs[b], dirs[a]);
            }

            bool carved = false;
            foreach (var d in dirs)
            {
                var v = Dir.V[Dir.ToIndex(d)];
                int nx = cur.x + v.x;
                int ny = cur.y + v.y;
                if (!map.InBounds(nx, ny) || visited[nx, ny]) continue;

                // carve (����� �� �ձ�)
                map[cur.x, cur.y].doors |= d;
                map[nx, ny].doors |= Dir.Opposite(d);

                visited[nx, ny] = true;
                stack.Push(new Vector2Int(nx, ny));
                carved = true;
                break;
            }

            if (!carved) stack.Pop();
        }

        // --- ���� �߰�(����): �� ������ ---
        int add = extraLinks > 0 ? extraLinks : (w * h) / 6;
        for (int i = 0; i < add; i++)
        {
            int x = rnd.Next(w);
            int y = rnd.Next(h);
            var options = new List<byte>();
            if (y + 1 < h) options.Add(Dir.N);
            if (x + 1 < w) options.Add(Dir.E);
            if (y - 1 >= 0) options.Add(Dir.S);
            if (x - 1 >= 0) options.Add(Dir.W);
            if (options.Count == 0) continue;
            var d = options[rnd.Next(options.Count)];
            var v = Dir.V[Dir.ToIndex(d)];
            int nx = x + v.x, ny = y + v.y;
            map[x, y].doors |= d;
            map[nx, ny].doors |= Dir.Opposite(d);
        }

        // �±�(Ȯ�� ����Ʈ): ����/����/���� ��
        map[start.x, start.y].tag = RoomTag.Start;

        return map;
    }

    public bool CanMove(byte d)
    {
        var c = this[current.x, current.y];
        return (c.doors & d) != 0;
    }

    public bool Move(byte d)
    {
        if (!CanMove(d)) return false;
        var v = Dir.V[Dir.ToIndex(d)];
        current = new Vector2Int(current.x + v.x, current.y + v.y);
        return true;
    }

    public void MarkVisited(Vector2Int pos)
    {
        var cell = this[pos.x, pos.y];
        cell.visited = true;
        this[pos.x, pos.y] = cell;
    }

    public void RevealAround(Vector2Int pos, int radius = 1)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = pos.x + dx, y = pos.y + dy;
                if (!InBounds(x, y)) continue;
                var cell = this[x, y];
                cell.discovered = true;
                this[x, y] = cell;
            }
    }
}
