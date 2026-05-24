using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;

public class YKNMMinimaxAI
{
    //private const int BoardWidth = 3;
    //private const int BoardHeight = 4;
    //private readonly PawnDataLibrary pawnDataLibrary;
    //private readonly ECampType aiCamp;
    //private readonly int maxDepth;

    //private int nodesExplored;
    //public YKNMMinimaxAI(ECampType aiCamp, PawnDataLibrary library, int depth = 4)
    //{
    //    this.aiCamp = aiCamp;
    //    this.pawnDataLibrary = library;
    //    this.maxDepth = depth;
    //}


    //public JobYokaiMove GetBestMove(YKNMGameState state)
    //{
    //    nodesExplored = 0;
    //    JobYokaiMove bestMove = null;
    //    int bestScore = int.MinValue;
    //    List<JobYokaiMove> moves = GetValidMoves(state, aiCamp);

    //    if (moves.Count == 0) return null;

    //    foreach (var move in moves)
    //    {
    //        YKNMGameState next = ApplyMove(state.Clone(), move);
    //        int score = Minimax(next, maxDepth - 1, int.MinValue, int.MaxValue, false);
    //        nodesExplored++;

    //        string moveLabel = move.ActionType == EActionType.PARACHUTE
    //            ? $"PARACHUTE {move.Pawn.Type} → {move.Destination}"
    //            : $"MOVE {move.Pawn.Type} {move.SourcePosition} → {move.Destination}";

    //        Debug.Log($"[Minimax] 🔍 Coup évalué : {moveLabel} | Score : {score}");

    //        if (score > bestScore)
    //        {
    //            bestScore = score;
    //            bestMove = move;
    //        }
    //    }

    //    Debug.Log($"[Minimax] 📊 Noeuds explorés : {nodesExplored} | Meilleur score : {bestScore}");
    //    return bestMove;
    //}

    //private int Minimax(YKNMGameState state, int depth, int alpha, int beta, bool isMaximizing)
    //{
    //    // 1. Switch state to terminal
    //    if (depth == 0 || IsTerminal(state))
    //        return Evaluate(state);

    //    ECampType currentCamp = isMaximizing ? aiCamp : Opponent(aiCamp);
    //    List<JobYokaiMove> moves = GetValidMoves(state, currentCamp);

    //    // if no moves available, evaluate the position (stalemate or checkmate)
    //    if (moves.Count == 0) return Evaluate(state);

    //    if (isMaximizing)
    //    {
    //        int best = int.MinValue;
    //        foreach (var move in moves)
    //        {
    //            YKNMGameState next = ApplyMove(state.Clone(), move);
    //            int score = Minimax(next, depth - 1, alpha, beta, false);
    //            best = Mathf.Max(best, score);
    //            alpha = Mathf.Max(alpha, best);
    //            if (alpha >= beta) break;
    //        }
    //        return best;
    //    }
    //    else
    //    {
    //        int best = int.MaxValue;
    //        foreach (var move in moves)
    //        {
    //            YKNMGameState next = ApplyMove(state.Clone(), move);
    //            int score = Minimax(next, depth - 1, alpha, beta, true);
    //            best = Mathf.Min(best, score);
    //            beta = Mathf.Min(beta, best);
    //            if (alpha >= beta) break;
    //        }
    //        return best;
    //    }
    //}

    //private List<JobYokaiMove> GetValidMoves(YKNMGameState state, ECampType camp)
    //{
    //    List<JobYokaiMove> moves = new List<JobYokaiMove>();

    //    // Move 
    //    foreach (var pawn in state.Board)
    //    {
    //        if (pawn.Owner != camp) continue;
    //        List<Vector2Int> dirs = pawnDataLibrary.GetDirectionsForType(pawn.Type, camp);

    //        foreach (var dir in dirs)
    //        {
    //            Vector2Int dest = pawn.Position + dir;
    //            if (!IsInBounds(dest)) continue;

    //            JobPawnState occupant = state.GetPawnAt(dest);
    //            if (occupant != null && occupant.Owner == camp) continue;

    //            moves.Add(new JobYokaiMove
    //            {
    //                Pawn = pawn,
    //                SourcePosition = pawn.Position,
    //                Destination = dest,
    //                ActionType = EActionType.MOVE
    //            });
    //        }
    //    }

    //    // Parachute
    //    foreach (var pawn in state.GetReserve(camp))
    //    {
    //        for (int x = 0; x < BoardWidth; x++)
    //        {
    //            for (int y = 0; y < BoardHeight; y++)
    //            {
    //                Vector2Int dest = new Vector2Int(x, y);
    //                if (state.IsCellEmpty(dest))
    //                {
    //                    moves.Add(new JobYokaiMove { Pawn = pawn, Destination = dest, ActionType = EActionType.PARACHUTE });
    //                }
    //            }
    //        }
    //    }

    //    return moves;
    //}

    //private YKNMGameState ApplyMove(YKNMGameState state, JobYokaiMove move)
    //{
    //    state.CurrentPlayer = Opponent(state.CurrentPlayer);

    //    if (move.ActionType == EActionType.PARACHUTE)
    //    {
    //        List<JobPawnState> reserve = state.GetReserve(move.Pawn.Owner);
    //        var pawnInReserve = reserve.Find(p => p.Type == move.Pawn.Type);
    //        if (pawnInReserve != null) reserve.Remove(pawnInReserve);

    //        state.Board.Add(new JobPawnState { Position = move.Destination, Type = move.Pawn.Type, Owner = move.Pawn.Owner });
    //    }
    //    else
    //    {
    //        var occupant = state.GetPawnAt(move.Destination);
    //        if (occupant != null)
    //        {
    //            state.Board.Remove(occupant);
    //            EPawnType capturedType = occupant.Type == EPawnType.KodamaSamurai ? EPawnType.Kodama : occupant.Type;
    //            state.GetReserve(move.Pawn.Owner).Add(new JobPawnState { Type = capturedType, Owner = move.Pawn.Owner });
    //        }

    //        var pawnOnBoard = state.Board.Find(p => p.Position == move.Pawn.Position);
    //        if (pawnOnBoard != null)
    //        {
    //            pawnOnBoard.Position = move.Destination;

    //            // DÉCOUPLAGE PROMOTION KODAMA : Basé dynamiquement sur le camp
    //            if (pawnOnBoard.Type == EPawnType.Kodama)
    //            {
    //                if (IsEnemyTerritory(pawnOnBoard.Owner, move.Destination))
    //                {
    //                    pawnOnBoard.Type = EPawnType.KodamaSamurai;
    //                }
    //            }
    //        }
    //    }

    //    return state;
    //}

    //private bool IsTerminal(YKNMGameState state)
    //{
    //    bool p1HasKing = state.Board.Exists(p => p.Type == EPawnType.Koropokkuru && p.Owner == ECampType.PLAYER_ONE);
    //    bool p2HasKing = state.Board.Exists(p => p.Type == EPawnType.Koropokkuru && p.Owner == ECampType.PLAYER_TWO);

    //    return !p1HasKing || !p2HasKing;
    //}

    //private int Evaluate(YKNMGameState state)
    //{
    //    ECampType opponent = Opponent(aiCamp);

    //    bool aiKingAlive = state.Board.Exists(p => p.Type == EPawnType.Koropokkuru && p.Owner == aiCamp);
    //    bool oppKingAlive = state.Board.Exists(p => p.Type == EPawnType.Koropokkuru && p.Owner == opponent);

    //    if (!aiKingAlive) return -10000;
    //    if (!oppKingAlive) return 10000;

    //    int score = 0;

    //    foreach (var pawn in state.Board)
    //    {
    //        int value = GetPieceValue(pawn.Type);

    //        if (pawn.Type == EPawnType.Koropokkuru)
    //        {
    //            // DÉCOUPLAGE VICTOIRE TRÔNE : Évalué dynamiquement
    //            if (IsEnemyTerritory(pawn.Owner, pawn.Position))
    //            {
    //                value += 5000;
    //            }
    //        }

    //        score += pawn.Owner == aiCamp ? value : -value;
    //    }

    //    foreach (var pawn in state.GetReserve(aiCamp)) score += GetPieceValue(pawn.Type) / 2;
    //    foreach (var pawn in state.GetReserve(opponent)) score -= GetPieceValue(pawn.Type) / 2;

    //    return score;
    //}

    //// Nouvelle méthode utilitaire partagée pour l'IA
    //private bool IsEnemyTerritory(ECampType camp, Vector2Int position)
    //{
    //    return (camp == ECampType.PLAYER_ONE && position.y == BoardHeight - 1) // y == 3
    //        || (camp == ECampType.PLAYER_TWO && position.y == 0);
    //}

    //private int GetPieceValue(EPawnType type) => type switch
    //{
    //    EPawnType.Koropokkuru => 1000,
    //    EPawnType.KodamaSamurai => 40,
    //    EPawnType.Kitsune => 30,
    //    EPawnType.Tanuki => 30,
    //    EPawnType.Kodama => 20,
    //    _ => 0
    //};

    //private bool IsInBounds(Vector2Int pos)
    //    => pos.x >= 0 && pos.x < BoardWidth && pos.y >= 0 && pos.y < BoardHeight;

    //private ECampType Opponent(ECampType camp)
    //    => camp == ECampType.PLAYER_ONE ? ECampType.PLAYER_TWO : ECampType.PLAYER_ONE;
}