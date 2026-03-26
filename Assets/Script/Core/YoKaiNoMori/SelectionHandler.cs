using System;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class SelectionHandler : MonoBehaviour
{
    private IInputService inputService;
    private IGameManager engine;
    private ITurnService turnService;

    private IPawn selectedPawn;

    private bool isParachuting = false;

    private void Start()
    {
        inputService = GameServiceLocator.Get<IInputService>();
        engine = GameServiceLocator.Get<IGameManager>();
        turnService = GameServiceLocator.Get<ITurnService>();

        inputService.OnCellLeftClicked += HandleCellClick;
        inputService.OnReservePawnClicked += HandleReservePawnClick;
    }

    private void HandleReservePawnClick(IPawn pawn)
    {
        // On vérifie que c'est bien le tour du proprio du pion
        if (pawn.GetCurrentOwner().GetCamp() == turnService.GetCurrentTurn())
        {
            selectedPawn = pawn;
            isParachuting = true;
            Debug.Log("Pion prêt à être parachuté !");
            // Tu peux ajouter un highlight visuel ici sur les cases vides
        }
    }

    private void HandleCellClick(CaseView caseView)
    {
        if (selectedPawn == null)
        {
            if (caseView.GetModel().IsBusy() && caseView.GetModel().GetPawnOnIt().GetCurrentOwner().GetCamp() == turnService.GetCurrentTurn())
            {
                selectedPawn = caseView.GetModel().GetPawnOnIt();
                isParachuting = false;
                caseView.Highlight(true);
            }
        }
        else
        {
            EActionType action = isParachuting ? EActionType.PARACHUTE : EActionType.MOVE;
            engine.DoAction(selectedPawn, caseView.GetModel().GetPosition(), action);

            selectedPawn = null;
            isParachuting = false;
        }
    }
}