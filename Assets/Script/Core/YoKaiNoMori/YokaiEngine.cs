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

    public YokaiEngine()
    {
        GameServiceLocator.Register<IGameManager>(this);
        gridService = GameServiceLocator.Get<IGridService>();
    }

    public void Init()
    {
      
    }

    public void Dispose()
    {
        GameServiceLocator.Unregister<IGameManager>();
    }

    public void DoAction(IPawn pawnTarget, Vector2Int destination, EActionType actionType)
    {
        // --- 1. VÉRIFICATION ---
        // Est-ce que la destination est valide pour ce pion ?
        List<Vector2Int> possibleMoves = GetValidMoves(pawnTarget);
        if (!possibleMoves.Contains(destination))
        {
            Debug.LogWarning("Mouvement invalide !");
            return;
        }

        // --- 2. EXÉCUTION ---
        IBoardCase targetCase = gridService.GetBoardCaseByPosition(destination);

        // Cas d'une capture
        if (targetCase.IsBusy())
        {
            Capture(targetCase.GetPawnOnIt(), pawnTarget.GetCurrentOwner());
        }

        // Déplacement physique (Logique)
        // On vide l'ancienne case
        pawnTarget.GetCurrentBoardCase().SetPawn(null);

        // On remplit la nouvelle
        targetCase.SetPawn(pawnTarget);

        // On met à jour la position interne du pion (Il faut ajouter SetPosition dans BoardPiece)
        ((BoardPiece)pawnTarget).SetPosition(destination, targetCase);

        // --- 3. RÈGLES SPÉCIALES & FIN DE TOUR ---
        CheckPromotion(pawnTarget, destination);
        CheckVictory();

        // On change le tour dans le GameState
        var turnService = GameServiceLocator.Get<ITurnService>();
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

    // 2. Changement de camp
    // Tu auras besoin d'un SetOwner() dans BoardPiece
        ((BoardPiece)victim).SetOwner(catcher);

        // 3. Envoyer dans la réserve
        // Tu devras créer un ReserveService plus tard pour stocker ces pions
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
        throw new System.NotImplementedException();
    }

    public List<Vector2Int> GetValidMoves(IPawn pawn)
    {
        List<Vector2Int> validPositions = new List<Vector2Int>();
        Vector2Int currentPos = pawn.GetCurrentPosition();

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
