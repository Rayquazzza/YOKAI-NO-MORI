using System;
using UnityEngine;

public class InputService : MonoBehaviour, IInputService
{
    public event Action<BoardCaseView> OnCellHoverChanged;
    public event Action<BoardCaseView> OnCellLeftClicked;
    public event Action<BoardCaseView> OnCellRightClicked;

    private BoardCaseView lastHovered;

    private IGameStateService gameStateService;


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
        if (gameStateService == null || gameStateService.GetCurrentGameState() != EGameState.IN_GAME) return;

        HandleMouseDetection();
    }


    private void HandleMouseDetection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            BoardCaseView current = hit.collider.GetComponentInParent<BoardCaseView>();

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
