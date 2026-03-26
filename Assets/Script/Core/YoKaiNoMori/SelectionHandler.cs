using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class SelectionHandler : MonoBehaviour
{
    private IInputService inputService;
    private IGameManager engine;
    private ITurnService turnService;

    private IPawn selectedPawn;

    private void Start()
    {
        inputService = GameServiceLocator.Get<IInputService>();
        engine = GameServiceLocator.Get<IGameManager>();
        turnService = GameServiceLocator.Get<ITurnService>();

        inputService.OnCellLeftClicked += HandleCellClick;
    }

    private void HandleCellClick(BoardCaseView caseView)
    {
        IBoardCase boardCase = caseView.GetModel(); 

        if (selectedPawn == null)
        {
            if (boardCase.IsBusy() && boardCase.GetPawnOnIt().GetCurrentOwner().GetCamp() == turnService.GetCurrentTurn())
            {
                selectedPawn = boardCase.GetPawnOnIt();
                caseView.Highlight(true);
            }
        }
        else
        {
            engine.DoAction(selectedPawn, boardCase.GetPosition(), EActionType.MOVE);
            selectedPawn = null;
        }
    }
}