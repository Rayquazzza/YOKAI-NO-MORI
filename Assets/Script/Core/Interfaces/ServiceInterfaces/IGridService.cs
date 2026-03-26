using System;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public interface IGridService :IDisposableService
{
    public event Action <Vector2Int> OnGridInitialized;
    void InitializeGrid(int width, int height);
    IBoardCase GetBoardCaseByPosition(Vector2Int position);
    IPawn GetPawnByPosition(Vector2Int position);
    List<IBoardCase> GetAllBoardCase();
    List<IPawn> GetAllPawn();
    void SpawnInitialPieces(ICompetitor p1, ICompetitor p2);
}
