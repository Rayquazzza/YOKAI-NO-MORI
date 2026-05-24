using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public struct PawnActionEvent
{
    static PawnActionEvent e;
    public IPawn Pawn;
    public Vector2Int Destination;
    public EActionType ActionType;
    public static void Trigger(IPawn pawn, Vector2Int destination, EActionType actionType)
    {
        e.Pawn = pawn;
        e.Destination = destination;
        e.ActionType = actionType;
        EventBus.Publish(e);
    }
}