using UnityEngine;
using YokaiNoMori.Interface;
public class BoardCase : IBoardCase
{
    private Vector2Int position;    

    private IPawn pawnOnIt;

    public BoardCase(Vector2Int position)
    {
        this.position = position;
    }

    public void SetPawn(IPawn pawn)
    {
        this.pawnOnIt = pawn;
    }

    public IPawn GetPawnOnIt()
    {
        return pawnOnIt;
    }

    public Vector2Int GetPosition()
    {
        return position;
    }

    public bool IsBusy()
    {
       return pawnOnIt != null;
    }
}
