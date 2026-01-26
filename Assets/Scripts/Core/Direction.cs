using UnityEngine;

public enum Direction
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
};
public static class DirectionUtility 
{
    public static Vector2Int GetDirection(int rotation)
    {
        switch (rotation)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.right;
            case 2: return Vector2Int.down;
            case 3: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }
    public static Vector2Int CW(Vector2Int dir)
    {
        return new Vector2Int(dir.y, -dir.x);
    }
}