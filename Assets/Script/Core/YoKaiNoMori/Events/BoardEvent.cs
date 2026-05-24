// =============================================================================
// BoardEvent.cs
// =============================================================================
// Struct d'événement unique utilisé pour toute la communication entre les systèmes.
// Utilise un enum BoardEventType pour identifier le type d'événement et des champs
// optionnels remplis selon le contexte (pion, destination, durée, etc.).
//
// Le champ Duration est rempli par le YKNMManager à partir du YKNMGameSettings.
// Il permet au front-end (BoardView, PawnView) de connaître la durée d'animation
// sans avoir de référence directe vers les settings ou le back-end.
// =============================================================================

using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public enum BoardEventType
{
    GridInitialized,
    PawnMoved,
    PawnCaptured,
    PawnCreated,
    PawnPromoted,
    ActionRequested,
    PawnSelected,
    TurnChanged,
    GameOver
}

public struct BoardEvent
{
    public BoardEventType EventType;
    public IPawn Pawn;
    public Vector2Int Destination;
    public Vector2Int Origin;
    public EActionType ActionType;
    public ICompetitor Competitor;
    public EPawnType PawnType;
    public ECampType Camp;
    public Vector2Int BoardSize;

    /// <summary>
    /// Durée d'animation associée à cet événement (en secondes).
    /// Rempli par le YKNMManager à partir du YKNMGameSettings.
    /// Le front-end l'utilise pour synchroniser ses animations.
    /// </summary>
    public float Duration;

    /// <summary>Courbe de tween pour l'animation associée.</summary>
    public Tools.MyTween.TweenType TweenType;

    static BoardEvent e;

    public static void Trigger(
        BoardEventType eventType,
        IPawn pawn = null,
        Vector2Int destination = default,
        Vector2Int origin = default,
        EActionType actionType = default,
        ICompetitor competitor = null,
        EPawnType pawnType = default,
        ECampType camp = default,
        Vector2Int boardSize = default,
        float duration = 0f,
        Tools.MyTween.TweenType tweenType = Tools.MyTween.TweenType.Linear)
    {
        e.EventType = eventType;
        e.Pawn = pawn;
        e.Destination = destination;
        e.Origin = origin;
        e.ActionType = actionType;
        e.Competitor = competitor;
        e.PawnType = pawnType;
        e.Camp = camp;
        e.BoardSize = boardSize;
        e.Duration = duration;
        e.TweenType = tweenType;
        EventBus.Publish(e);
    }
}