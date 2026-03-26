using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class BoardPiece : IPawn
{

    private Vector2Int position;

    private ICompetitor owner;

    private IBoardCase boardCase;

    private EPawnType pawnType; 

    private List<Vector2Int> directions;

    public BoardPiece(SBoardPieceInstance instance)
    {
        this.position = instance.position;
        this.owner = instance.owner;
        this.boardCase = instance.boardCase;
        this.pawnType = instance.pawnType;
        this.directions = instance.directions;
    }

    public IBoardCase GetCurrentBoardCase() => boardCase;

    public ICompetitor GetCurrentOwner() => owner;

    public Vector2Int GetCurrentPosition() => position;

    public List<Vector2Int> GetDirections()
    {
       return directions;
    }

    public EPawnType GetPawnType()
    {
      return pawnType;
    }

    public void Promote()
    {
       
    }
    public void SetOwner(ICompetitor newOwner)
    {
        owner = newOwner;
    }

    public void SetPosition(Vector2Int newPosition, IBoardCase newBoardCase)
    {
        position = newPosition;
        boardCase = newBoardCase;
    }
}
