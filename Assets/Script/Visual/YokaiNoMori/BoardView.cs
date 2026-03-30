using System;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class BoardView : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private CaseView casePrefab;
    [SerializeField] private PawnView playerPawnPrefab;

    [Space(10)]
    [Header("Grid Settings")]
    [SerializeField] private float spacing = 1.1f;
    [SerializeField] private GameObject caseParent;
    [SerializeField] private GameObject pawnParent;
    [SerializeField] private RetrieveView RetrieveView;

    [Space(10)]
    [Header("Visuals")]
    [SerializeField] private PawnVisualRegistry pawnVisualRegistry;

    private Dictionary<IPawn, PawnView> pawnMap = new Dictionary<IPawn, PawnView>();
    private Dictionary<Vector2Int, CaseView> caseMap = new Dictionary<Vector2Int, CaseView>();

    private int width;
    private int height;

    private IGridService gridService;

    private void Awake()
    {
        gridService = GameServiceLocator.Get<IGridService>();

        gridService.OnGridInitialized += CreateVisualGrid;
        gridService.OnPawnCreated += HandlePawnCreated;
        gridService.OnPawnCaptured += HandleCaptureVisual;
        gridService.OnPawnMoved += HandlePawnMoved;
    }

    private void HandlePawnMoved(IPawn pawn, Vector2Int newPosition)
    {
        if (pawnMap.TryGetValue(pawn, out PawnView visualPawn))
        {
            Vector3 targetWorldPos = GetWorldPosition(width, height, spacing, newPosition.x, newPosition.y);

            visualPawn.MoveTo(targetWorldPos);
        }
    }

    private void HandleCaptureVisual(IPawn victim, ICompetitor catcher)
    {
        if (pawnMap.TryGetValue(victim, out PawnView visualPawn))
        {
            int index = catcher.GetReserve().Count - 1;
            Vector3 targetPos = RetrieveView.GetReservePosition(index, catcher.GetCamp());

            visualPawn.MoveToReserve(targetPos, catcher.GetCamp());
        }
    }

    private void HandlePawnCreated(SOnPawnCreated data)
    {
        CreateVisualPawn(data);
    }

    private void CreateVisualPawn(SOnPawnCreated data)
    {
        Vector2Int logicalPos = data.Position;
        Vector3 worldPos = GetWorldPosition(width, height, spacing, logicalPos.x, logicalPos.y);

        Quaternion rotation = Quaternion.identity;

        if (data.Owner.GetCamp() == ECampType.PLAYER_TWO)
        {
            rotation = Quaternion.Euler(0, 180, 0);
        }

        PawnView pawnInstance = Instantiate(playerPawnPrefab, worldPos, rotation, pawnParent.transform);

        pawnInstance.name = $"Pawn_{logicalPos.x}_{logicalPos.y}";
        pawnMap.Add(data.Pawn, pawnInstance);

        Sprite pawnSprite = pawnVisualRegistry.GetPawnSprite(data.PawnType);
        pawnInstance.Setup(pawnSprite);
    }

    private void CreateVisualGrid(Vector2Int size)
    {
        width = size.x;
        height = size.y;

        List<IBoardCase> allCases = gridService.GetAllBoardCase();


        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector2Int logicalPos = new Vector2Int(x, z);
                IBoardCase boardCase = gridService.GetBoardCaseByPosition(logicalPos);

                if (boardCase != null)
                {
                    Vector3 worldPos = GetWorldPosition(width, height, spacing, x, z);

                    CaseView caseInstance = Instantiate(casePrefab, worldPos, Quaternion.identity, caseParent.transform);

                    caseInstance.Setup(boardCase);

                    caseInstance.name = $"Case_{x}_{z}";
                    caseMap.Add(logicalPos, caseInstance);
                }
            }
        }
    }

    private Vector3 GetWorldPosition(int width, int height, float spacing, int x, int z)
    {
        float offsetX = (width - 1) * spacing / 2f;
        float offsetZ = (height - 1) * spacing / 2f;
        return new Vector3(x * spacing - offsetX, 0, z * spacing - offsetZ);
    }

    private void OnDestroy()
    {
        // Toujours se désabonner des événements quand l'objet est détruit
        if (gridService == null) return;
        
        gridService.OnGridInitialized -= CreateVisualGrid;
        gridService.OnPawnCreated -= HandlePawnCreated;
        gridService.OnPawnCaptured -= HandleCaptureVisual;
        gridService.OnPawnMoved -= HandlePawnMoved;

    }
}