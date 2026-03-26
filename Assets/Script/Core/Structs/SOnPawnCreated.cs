using UnityEngine;
using UnityEngine.Rendering.Universal;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public struct SOnPawnCreated 
{
    public readonly Vector2Int Position;
    public readonly IPawn Pawn;
    public readonly ICompetitor Owner;
    public readonly EPawnType PawnType;

    public SOnPawnCreated(IPawn pawn, Vector2Int position, ICompetitor owner, EPawnType pawnType)
    {
        Pawn = pawn;
        Position = position;
        Owner = owner;
        PawnType = pawnType;
    }
}
