using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public struct SBoardPieceInstance 
{
    public readonly Vector2Int position;

    public readonly ICompetitor owner;

    public readonly IBoardCase boardCase;

    public readonly EPawnType pawnType;

    public readonly List<Vector2Int> directions;

    public SBoardPieceInstance(IPawn pawn, Vector2Int position, ICompetitor owner, IBoardCase boardCase, EPawnType pawnType, List<Vector2Int> directions)
    {
        this.position = position;
        this.owner = owner;
        this.boardCase = boardCase;
        this.pawnType = pawnType;
        this.directions = directions;
    }
}
