using System;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;
using YokaiNoMori.Struct;

public class YokaiEngine : IGameManager, IDisposableService
{
    private SAction lastAction;

    private IGridService gridService;
    private ITurnService turnService;

    public YokaiEngine()
    {
        GameServiceLocator.Register<IGameManager>(this);
        
    }

    public void Init()
    {
        gridService = GameServiceLocator.Get<IGridService>();
        turnService = GameServiceLocator.Get<ITurnService>();
    }

    public void Dispose()
    {
        GameServiceLocator.Unregister<IGameManager>();
    }

    public void DoAction(IPawn pawnTarget, Vector2Int destination, EActionType actionType)
    {

        IBoardCase targetCase = gridService.GetBoardCaseByPosition(destination);

        if (actionType == EActionType.PARACHUTE)
        {
            // --- LOGIQUE PARACHUTAGE ---
            if (targetCase.IsBusy()) return; // On ne peut pas parachuter sur quelqu'un

            // 1. Retirer de la réserve du joueur
            pawnTarget.GetCurrentOwner().RemoveFromReserve(pawnTarget);

            // 2. Placer sur la nouvelle case
            targetCase.SetPawn(pawnTarget);
            ((BoardPiece)pawnTarget).SetPosition(destination, targetCase);

            // 3. Alerter la vue (on réutilise le même trigger car le pion "bouge" vers la case)
            (gridService as BoardGridService)?.TriggerOnPawnMoved(pawnTarget, destination);
        }
        else if(actionType == EActionType.MOVE)
        {
            // --- 1. VÉRIFICATION ---
            List<Vector2Int> possibleMoves = GetValidMoves(pawnTarget);
            if (!possibleMoves.Contains(destination)) return;

            // --- 2. EXÉCUTION ---
            IBoardCase oldCase = pawnTarget.GetCurrentBoardCase();

            // Cas d'une capture
            if (targetCase.IsBusy())
            {
                IPawn victim = targetCase.GetPawnOnIt();
                Capture(victim, pawnTarget.GetCurrentOwner());

                // CRUCIAL : On vide la case cible car le mort part en réserve !
                targetCase.SetPawn(null);
            }

            // On vide l'ancienne case
            oldCase.SetPawn(null);

            // On remplit la nouvelle
            targetCase.SetPawn(pawnTarget);

            // On met à jour la donnée du pion (position interne)
            ((BoardPiece)pawnTarget).SetPosition(destination, targetCase);

            (gridService as BoardGridService)?.TriggerOnPawnMoved(pawnTarget, destination);
        }
           

        // ... suite du code (LastAction, Turn, etc.)

        lastAction.SetAction(pawnTarget.GetCurrentOwner().GetCamp(), pawnTarget.GetPawnType(), actionType, pawnTarget.GetCurrentPosition(), destination, targetCase.GetPawnOnIt());

        Debug.Log($"Action effectuée : {actionType} - {pawnTarget.GetPawnType()} déplacé de {lastAction.StartPosition} à {lastAction.NewPosition}");


        // --- 3. RÈGLES SPÉCIALES & FIN DE TOUR ---

        CheckPromotion(pawnTarget, destination);

        CheckVictory();

        turnService.SwitchTurn();

    }



    private void CheckVictory()
    {

    }

    private void CheckPromotion(IPawn pawnTarget, Vector2Int destination)
    {

    }

    private void Capture(IPawn victim, ICompetitor catcher)
    {
        // 1. Reset de la promotion (Si c'était un Samouraï, il redevient Kodama)
        if (victim.GetPawnType() == EPawnType.KodamaSamurai)
        {
            // Tu auras besoin d'une méthode ResetPromotion() dans BoardPiece
        }

        catcher.AddToReserve(victim);

        // 2. Changement de camp
        // Tu auras besoin d'un SetOwner() dans BoardPiece
        ((BoardPiece)victim).SetOwner(catcher);

        (gridService as BoardGridService)?.TriggerOnPawnCaptured(victim, catcher);

        catcher.AddToReserve(victim);

        Debug.Log($"{victim.GetPawnType()} capturé par {catcher.GetCamp()}");
    }

    public List<IBoardCase> GetAllBoardCase()
    {
        return gridService.GetAllBoardCase();
    }

    public List<IPawn> GetAllPawn()
    {
        return gridService.GetAllPawn();
    }

    public SAction GetLastAction()
    {
        return lastAction;
    }

    public List<IPawn> GetPawnsOnBoard(ECampType campType)
    {
        throw new System.NotImplementedException();
    }

    public List<IPawn> GetReservePawnsByPlayer(ECampType campType)
    {
        var player = GameServiceLocator.Get<IPlayersService>().GetPlayer(campType);
        return player.GetReserve();
    }

    public List<Vector2Int> GetValidMoves(IPawn pawn)
    {
        Vector2Int currentPos = pawn.GetCurrentPosition();
        List<Vector2Int> validPositions = new List<Vector2Int>();

        foreach (Vector2Int dir in pawn.GetDirections())
        {
            Vector2Int targetPos = currentPos + dir;

            // 1. Est-ce sur le plateau ?
            IBoardCase targetCase = gridService.GetBoardCaseByPosition(targetPos);
            if (targetCase == null) continue;

            // 2. Est-ce occupé par un allié ?
            if (targetCase.IsBusy())
            {
                if (targetCase.GetPawnOnIt().GetCurrentOwner() == pawn.GetCurrentOwner())
                    continue; // On ne peut pas manger son propre camp
            }

            validPositions.Add(targetPos);
        }
        return validPositions;
    }


}
