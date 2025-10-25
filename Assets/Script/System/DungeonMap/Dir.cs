using UnityEngine;

public static class Dir
{
    public const byte N = 1 << 0; // 1
    public const byte E = 1 << 1; // 2
    public const byte S = 1 << 2; // 4
    public const byte W = 1 << 3; // 8

    public static readonly Vector2Int[] V =
    {
        new Vector2Int(0,  1), // N (bit 0)
        new Vector2Int(1,  0), // E (bit 1)
        new Vector2Int(0, -1), // S (bit 2)
        new Vector2Int(-1, 0), // W (bit 3)
    };

    public static byte Opposite(byte d)
    {
        if (d == N) return S;
        if (d == E) return W;
        if (d == S) return N;
        if (d == W) return E;
        return 0;
    }

    public static int ToIndex(byte d) =>
        d == N ? 0 : d == E ? 1 : d == S ? 2 : d == W ? 3 : -1;
}
