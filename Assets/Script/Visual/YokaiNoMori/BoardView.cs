using UnityEngine;
using YokaiNoMori.Interface;
using System.Collections.Generic;
using System;

public class BoardView : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private BoardCaseView casePrefab; 
    [Header("Settings")]
    [SerializeField] private float spacing = 1.1f; 

    private IGridService gridService;

    private void Awake()
    {
        // On récupère le service dès le réveil
        gridService = GameServiceLocator.Get<IGridService>();

        // S'abonner à l'événement
        gridService.OnGridInitialized += CreateVisualGrid;
    }

    private void CreateVisualGrid(Vector2Int size)
    {
        int width = size.x;
        int height = size.y;

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

                    BoardCaseView caseInstance = Instantiate(casePrefab, worldPos, Quaternion.identity, this.transform);

                    caseInstance.Setup(boardCase);

                    caseInstance.name = $"Case_{x}_{z}";
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
        if (gridService != null)
        {
            gridService.OnGridInitialized -= CreateVisualGrid;
        }
    }
}