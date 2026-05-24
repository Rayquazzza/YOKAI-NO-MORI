using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class SelectionHandler : MonoBehaviour
{
    private IInputService inputService;
    private ITurnService turnService;
    private IPawn selectedPawn;

    private void Start()
    {
        inputService = GameServiceLocator.Get<IInputService>();
        turnService = GameServiceLocator.Get<ITurnService>();
        inputService.OnCellLeftClicked += HandleBoardClick;
        inputService.OnPawnClicked += HandleReserveClick;
    }

    private void HandleBoardClick(CaseView caseView)
    {
        IBoardCase boardCase = caseView.GetModel();
        ECampType currentTurn = turnService.GetCurrentTurn();

        if (selectedPawn == null)
        {
            if (boardCase.IsBusy() && boardCase.GetPawnOnIt().GetCurrentOwner().GetCamp() == currentTurn)
                SelectPawn(boardCase.GetPawnOnIt());
            return;
        }

        if (boardCase.IsBusy() && boardCase.GetPawnOnIt().GetCurrentOwner().GetCamp() == currentTurn)
        {
            SelectPawn(boardCase.GetPawnOnIt());
        }
        else
        {
            EActionType action = GetActionType(selectedPawn);
            BoardEvent.Trigger(BoardEventType.ActionRequested,
                pawn: selectedPawn,
                destination: boardCase.GetPosition(),
                actionType: action);
            DeselectPawn();
        }
    }

    private void HandleReserveClick(PawnView pawnView)
    {
        IPawn clickedPawn = pawnView.GetModel();
        if (clickedPawn.GetCurrentOwner().GetCamp() == turnService.GetCurrentTurn())
            SelectPawn(clickedPawn);
    }

    private void SelectPawn(IPawn pawn)
    {
        selectedPawn = pawn;
        BoardEvent.Trigger(BoardEventType.PawnSelected, pawn: pawn);
    }

    private void DeselectPawn() => selectedPawn = null;

    private EActionType GetActionType(IPawn pawn)
        => pawn.GetCurrentOwner().GetReserve().Contains(pawn) ? EActionType.PARACHUTE : EActionType.MOVE;

    private void OnDestroy()
    {
        if (inputService == null) return;
        inputService.OnCellLeftClicked -= HandleBoardClick;
        inputService.OnPawnClicked -= HandleReserveClick;
    }
}