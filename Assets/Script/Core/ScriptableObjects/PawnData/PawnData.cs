using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using System;
[Serializable]
public struct SerializableVector2Int
{
    public int x, y;
    public Vector2Int ToVector2Int() => new Vector2Int(x, y);
}

[CreateAssetMenu(fileName = "PawnData", menuName = "YokaiNoMori/PawnData")]
public class PawnData : ScriptableObject
{
    [InspectorGroup("GENERAL", true, 22)]
    public EPawnType pawnType;
    public Sprite sprite;

    [InspectorGroup("DIRECTIONS", true, 22)]
    public EDirections directionsP1; // Case à cocher dans Unity
    public EDirections directionsP2; // Case à cocher dans Unity (Axe inversé ou miroir)

    /// <summary>
    /// Convertit les bits cochés en liste de Vector2Int pour ton moteur actuel
    /// </summary>
    public List<Vector2Int> GetConvertDirs(ECampType camp)
    {
        EDirections flags = (camp == ECampType.PLAYER_ONE) ? directionsP1 : directionsP2;
        List<Vector2Int> vectors = new List<Vector2Int>();

        if (flags.HasFlag(EDirections.North)) vectors.Add(new Vector2Int(0, 1));
        if (flags.HasFlag(EDirections.NorthEast)) vectors.Add(new Vector2Int(1, 1));
        if (flags.HasFlag(EDirections.East)) vectors.Add(new Vector2Int(1, 0));
        if (flags.HasFlag(EDirections.SouthEast)) vectors.Add(new Vector2Int(1, -1));
        if (flags.HasFlag(EDirections.South)) vectors.Add(new Vector2Int(0, -1));
        if (flags.HasFlag(EDirections.SouthWest)) vectors.Add(new Vector2Int(-1, -1));
        if (flags.HasFlag(EDirections.West)) vectors.Add(new Vector2Int(-1, 0));
        if (flags.HasFlag(EDirections.NorthWest)) vectors.Add(new Vector2Int(-1, 1));

        return vectors;
    }
}