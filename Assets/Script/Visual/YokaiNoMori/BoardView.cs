// =============================================================================
// BoardView.cs
// =============================================================================
// Front-end visuel du plateau. Écoute les BoardEvent publiés par le YKNMManager
// et met à jour les visuels (instanciation, déplacement, capture, promotion).
//
// COORDINATION TEMPORELLE :
// Chaque BoardEvent contient un champ Duration et TweenType remplis par le
// YKNMManager à partir du YKNMGameSettings. BoardView les passe directement
// au PawnView qui les utilise pour configurer son MyFeedbackTweenPosition.
// Résultat : le visuel et la logique sont synchronisés sans couplage direct.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class BoardView : MonoBehaviour, IEventListener<BoardEvent>
{
    [InspectorGroup("Prefabs", true, 22)]
    [SerializeField] private CaseView casePrefab;
    [SerializeField] private PawnView playerPawnPrefab;

    [InspectorGroup("Grid Settings", true, 36)]
    [SerializeField] private float spacing = 1.1f;
    [SerializeField] private GameObject caseParent;
    [SerializeField] private GameObject pawnParent;
    [SerializeField] private RetrieveView retrieveView;

    [InspectorGroup("Visuals", true, 54)]
    [SerializeField] private PawnDataLibrary pawnDataLibrary;

    private Dictionary<IPawn, PawnView> pawnMap = new Dictionary<IPawn, PawnView>();
    private Dictionary<Vector2Int, CaseView> caseMap = new Dictionary<Vector2Int, CaseView>();

    private int width;
    private int height;
    private IGridService gridService;

    private void Awake()
    {
        gridService = GameServiceLocator.Get<IGridService>();
    }

    private void OnEnable() => this.EventStartListening<BoardEvent>();
    private void OnDisable() => this.EventStopListening<BoardEvent>();

    // =========================================================================
    // DISPATCH DES ÉVÉNEMENTS
    // =========================================================================

    public void OnEvent(BoardEvent e)
    {
        switch (e.EventType)
        {
            case BoardEventType.GridInitialized:
                CreateVisualGrid(e.BoardSize);
                break;
            case BoardEventType.PawnMoved:
                HandlePawnMoved(e.Pawn, e.Destination, e.Duration, e.TweenType);
                break;
            case BoardEventType.PawnCaptured:
                HandleCaptureVisual(e.Pawn, e.Competitor, e.Duration, e.TweenType);
                break;
            case BoardEventType.PawnCreated:
                HandlePawnCreated(e.Pawn, e.Origin, e.PawnType, e.Competitor);
                break;
            case BoardEventType.PawnPromoted:
                HandlePawnPromoted(e.Pawn);
                break;
        }
    }

    // =========================================================================
    // GESTION DES ÉVÉNEMENTS VISUELS
    // =========================================================================

    /// <summary>
    /// Anime le déplacement d'un pion vers sa nouvelle position.
    /// La durée et la courbe viennent du BoardEvent (rempli par YKNMManager).
    /// </summary>
    private void HandlePawnMoved(IPawn pawn, Vector2Int newPosition, float duration, Tools.MyTween.TweenType tweenType)
    {
        if (pawnMap.TryGetValue(pawn, out PawnView visualPawn))
        {
            Vector3 targetWorldPos = GetWorldPosition(width, height, spacing, newPosition.x, newPosition.y);
            visualPawn.MoveTo(targetWorldPos, duration, tweenType);
        }
    }

    /// <summary>
    /// Anime la capture : met à jour le sprite puis déplace le pion vers la réserve.
    /// </summary>
    private void HandleCaptureVisual(IPawn victim, ICompetitor catcher, float duration, Tools.MyTween.TweenType tweenType)
    {
        if (pawnMap.TryGetValue(victim, out PawnView visualPawn))
        {
            // Le pion capturé redevient visuellement un Kodama si nécessaire
            Sprite baseSprite = pawnDataLibrary.GetByType(victim.GetPawnType())?.sprite;
            visualPawn.UpdateSprite(baseSprite);

            int index = catcher.GetReserve().Count - 1;
            Vector3 targetPos = retrieveView.GetReservePosition(index, catcher.GetCamp());
            visualPawn.MoveToReserve(targetPos, catcher.GetCamp(), duration, tweenType);
        }
    }

    /// <summary>Instancie le visuel d'un nouveau pion sur le plateau.</summary>
    private void HandlePawnCreated(IPawn pawn, Vector2Int position, EPawnType pawnType, ICompetitor owner)
    {
        Vector3 worldPos = GetWorldPosition(width, height, spacing, position.x, position.y);
        Quaternion rotation = owner.GetCamp() == ECampType.PLAYER_TWO
            ? Quaternion.Euler(0, 180, 0)
            : Quaternion.identity;

        PawnView pawnInstance = Instantiate(playerPawnPrefab, worldPos, rotation, pawnParent.transform);
        pawnInstance.name = $"Pawn_{position.x}_{position.y}";
        pawnMap.Add(pawn, pawnInstance);

        Sprite pawnSprite = pawnDataLibrary.GetByType(pawnType)?.sprite;
        pawnInstance.Setup(pawnSprite, pawn);
    }

    /// <summary>Met à jour le sprite d'un pion promu.</summary>
    private void HandlePawnPromoted(IPawn pawn)
    {
        if (pawnMap.TryGetValue(pawn, out PawnView visualPawn))
        {
            Sprite promotedSprite = pawnDataLibrary.GetByType(EPawnType.KodamaSamurai)?.sprite;
            visualPawn.UpdateSprite(promotedSprite);
        }
    }

    // =========================================================================
    // CONSTRUCTION DE LA GRILLE
    // =========================================================================

    private void CreateVisualGrid(Vector2Int size)
    {
        width = size.x;
        height = size.y;

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

    /// <summary>
    /// Convertit des coordonnées logiques (x, z) en position monde centrée sur (0,0).
    /// </summary>
    private Vector3 GetWorldPosition(int width, int height, float spacing, int x, int z)
    {
        float offsetX = (width - 1) * spacing / 2f;
        float offsetZ = (height - 1) * spacing / 2f;
        return new Vector3(x * spacing - offsetX, 0, z * spacing - offsetZ);
    }
}