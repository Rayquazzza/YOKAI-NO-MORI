using System;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class BoardGridService : IGridService
{

    private Dictionary<Vector2Int, IBoardCase> boardCases = new Dictionary<Vector2Int, IBoardCase>();
    private int width = 3;
    private int height = 4;

    private PawnDataLibrary pawnDataLibrary;

    public BoardGridService(PawnDataLibrary pawnDataLibrary)
    {
        GameServiceLocator.Register<IGridService>(this);
        this.pawnDataLibrary = pawnDataLibrary;
    }

    public void Init()
    {
        
    }

    public void Dispose()
    {
        GameServiceLocator.Unregister<IGridService>();
    }

    public void InitializeGrid(int width, int height)
    {
        this.width = width;
        this.height = height;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                boardCases[position] = new BoardCase(position);
            }
        }

        Vector2Int gridSize = new Vector2Int(width, height);
        BoardEvent.Trigger(BoardEventType.GridInitialized, boardSize: gridSize);

    }

    public IBoardCase GetBoardCaseByPosition(Vector2Int position)
    {
        return boardCases.TryGetValue(position, out IBoardCase boardCase) ? boardCase : null;
    }

    public IPawn GetPawnByPosition(Vector2Int position)
    {
        return boardCases.TryGetValue(position, out IBoardCase boardCase) ? boardCase.GetPawnOnIt() : null;
    }

    public List<IBoardCase> GetAllBoardCase()
    {
        return new List<IBoardCase>(boardCases.Values);
    }

    public List<IPawn> GetAllPawn()
    {
        List<IPawn> pawns = new List<IPawn>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                IPawn pawn = GetPawnByPosition(position);
                if (pawn != null)
                {
                    pawns.Add(pawn);
                }
            }
        }

        return pawns;
    }

    public void SpawnInitialPieces(ICompetitor p1, ICompetitor p2)
    {
        // --- JOUEUR 1 
        CreateAndPlacePawn(new Vector2Int(0, 0), p1, EPawnType.Tanuki);
        CreateAndPlacePawn(new Vector2Int(1, 0), p1, EPawnType.Koropokkuru);
        CreateAndPlacePawn(new Vector2Int(2, 0), p1, EPawnType.Kitsune);
        CreateAndPlacePawn(new Vector2Int(1, 1), p1, EPawnType.Kodama);

        // --- JOUEUR 2 
        CreateAndPlacePawn(new Vector2Int(0, 3), p2, EPawnType.Kitsune);
        CreateAndPlacePawn(new Vector2Int(1, 3), p2, EPawnType.Koropokkuru);
        CreateAndPlacePawn(new Vector2Int(2, 3), p2, EPawnType.Tanuki);
        CreateAndPlacePawn(new Vector2Int(1, 2), p2, EPawnType.Kodama);
    }

    private void CreateAndPlacePawn(Vector2Int pos, ICompetitor owner, EPawnType type)
    {
        List<Vector2Int> dirs = GetDirectionsForType(type, owner.GetCamp());

        var caseRef = GetBoardCaseByPosition(pos);
        var instanceData = new SBoardPieceInstance(null, pos, owner, caseRef, type, dirs);
        BoardPiece newPawn = new BoardPiece(instanceData);

        caseRef.SetPawn(newPawn);

        var structData = new SOnPawnCreated(newPawn, pos, owner, type);
        BoardEvent.Trigger(BoardEventType.PawnCreated, pawn: newPawn, origin: pos, competitor: owner, pawnType: type);
    }

    private List<Vector2Int> GetDirectionsForType(EPawnType type, ECampType camp)
    {
        if (pawnDataLibrary == null)
        {
            Debug.LogError("[BoardGridService] La PawnDataLibrary est manquante ou nulle !");
            return new List<Vector2Int>();
        }

        return pawnDataLibrary.GetDirectionsForType(type, camp);
    }

}
