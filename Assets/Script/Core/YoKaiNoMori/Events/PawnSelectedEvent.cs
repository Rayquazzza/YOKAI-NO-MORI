using YokaiNoMori.Interface;

public struct PawnSelectedEvent
{
    static PawnSelectedEvent e;
    public IPawn Pawn;
    public static void Trigger(IPawn pawn)
    {
        e.Pawn = pawn;
        EventBus.Publish(e);
    }
}