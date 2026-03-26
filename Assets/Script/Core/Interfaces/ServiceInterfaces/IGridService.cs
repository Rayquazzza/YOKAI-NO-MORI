using System;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public interface IGridService :IDisposableService
{
    //EVENTS
    public event Action <Vector2Int> OnGridInitialized;
    public event Action<SOnPawnCreated> OnPawnCreated;
    public event Action<IPawn, ICompetitor> OnPawnCaptured;
    public event Action<IPawn, Vector2Int> OnPawnMoved;


    //METHODS
    void InitializeGrid(int width, int height);
    IBoardCase GetBoardCaseByPosition(Vector2Int position);
    IPawn GetPawnByPosition(Vector2Int position);
    List<IBoardCase> GetAllBoardCase();
    List<IPawn> GetAllPawn();
    void SpawnInitialPieces(ICompetitor p1, ICompetitor p2);
}
