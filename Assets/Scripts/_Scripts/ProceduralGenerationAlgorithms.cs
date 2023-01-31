using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ProceduralGenerationAlgorithms
{
    
    public static HashSet<Vector2Int> SimpleRandomWalk(Vector2Int startPosition, int walkLenght)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();

        path.Add(startPosition);
        var previousposition = startPosition;

        for (int i = 0; i < walkLenght; i++)
        {
            var newPosition = previousposition + Direction2D.GetRandomCardinalDirection();
            path.Add(newPosition);
            previousposition = newPosition;
        }
        return path;
    }
}

public static class Direction2D
{
    public static List<Vector2Int> cardinalDirectionList = new List<Vector2Int>
    {
        new Vector2Int(0,1), //Up
        new Vector2Int(1,0), //RIGHT
        new Vector2Int(0,-1), //DOWN
        new Vector2Int(-1,0) // LEFT
    };

    public static Vector2Int GetRandomCardinalDirection()
    {
        return cardinalDirectionList[Random.Range(0, cardinalDirectionList.Count)];
    }
}
