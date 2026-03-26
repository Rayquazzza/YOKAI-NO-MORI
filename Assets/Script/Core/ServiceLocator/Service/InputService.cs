using System;
using UnityEngine;
using YokaiNoMori.Interface;

public class InputService : MonoBehaviour, IInputService
{
    public event Action<CaseView> OnCellHoverChanged;
    public event Action<CaseView> OnCellLeftClicked;
    public event Action<CaseView> OnCellRightClicked;
    public event Action<IPawn> OnReservePawnClicked;

    private CaseView lastHovered;

    private IGameStateService gameStateService;

   [SerializeField] private LayerMask boardLayerMask;


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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, boardLayerMask))
        {
            CaseView current = hit.collider.GetComponentInParent<CaseView>();

            if (current != lastHovered)
            {
                lastHovered?.Highlight(false);
                lastHovered = current;

                if (current != null)
                {
                    lastHovered.Highlight(true);
                    OnCellHoverChanged?.Invoke(lastHovered);
                }
            }

            if (current != null)
            {
                if (Input.GetMouseButtonDown(0)) OnCellLeftClicked?.Invoke(current);
                if (Input.GetMouseButtonDown(1)) OnCellRightClicked?.Invoke(current);
            }
        }
        else { ClearHover(); }
    }

    private void ClearHover()
    {
        if (lastHovered != null)
        {
            lastHovered.Highlight(false);
            lastHovered = null;
            OnCellHoverChanged?.Invoke(null);
        }
    }

    private void OnDestroy()
    {
        GameServiceLocator.Unregister<IInputService>();
    }
}
