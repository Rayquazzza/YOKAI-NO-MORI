using System;
using UnityEngine;
using YokaiNoMori.Interface;

public class InputService : MonoBehaviour, IInputService
{
    public event Action<CaseView> OnCellHoverChanged;
    public event Action<CaseView> OnCellLeftClicked;
    public event Action<PawnView> OnPawnClicked;

    private PawnView lastHovered;

    private IGameStateService gameStateService;

    [SerializeField] private LayerMask pawnLayerMask;


    private void Awake()
    {
        GameServiceLocator.Register<IInputService>(this);
    }

    private void Start()
    {
        gameStateService = GameServiceLocator.Get<IGameStateService>();
    }

    private void Update()
    {
        //if (gameStateService == null || gameStateService.GetCurrentGameState() != EGameState.IN_GAME) return;

        HandleMouseDetection();
    }


    private void HandleMouseDetection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, pawnLayerMask);

        PawnView clickedPawn = null;
        CaseView clickedCase = null;

        foreach (var hit in hits)
        {
            if (clickedPawn == null) clickedPawn = hit.collider.GetComponent<PawnView>();
            if (clickedCase == null) clickedCase = hit.collider.GetComponent<CaseView>();
        }

        if (clickedPawn != null && clickedCase == null)
        {
            OnPawnClicked?.Invoke(clickedPawn);
        }
        else if (clickedCase != null)
        {
            OnCellLeftClicked?.Invoke(clickedCase);
        }
    }

    private void ClearHover()
    {
        if (lastHovered != null)
        {
            //lastHovered.Highlight(false);
            lastHovered = null;
            OnCellHoverChanged?.Invoke(null);
        }
    }

    private void OnDestroy()
    {
        GameServiceLocator.Unregister<IInputService>();
    }
}
