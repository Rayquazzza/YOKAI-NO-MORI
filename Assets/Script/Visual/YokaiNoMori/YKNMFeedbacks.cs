using UnityEngine;

public class YKNMFeedbacks : MonoBehaviour, IEventListener<BoardEvent>
{
    [InspectorGroup("Pawn Events",true,22,true)]
    [SerializeField] private MyFeedbackPlayer onPawnMoved;
    [SerializeField] private MyFeedbackPlayer onPawnCaptured;
    [SerializeField] private MyFeedbackPlayer onPawnPromoted;
    [SerializeField] private MyFeedbackPlayer onGameOver;

    private void OnEnable() => this.EventStartListening<BoardEvent>();
    private void OnDisable() => this.EventStopListening<BoardEvent>();

    public void OnEvent(BoardEvent e)
    {
        switch (e.EventType)
        {
            case BoardEventType.PawnMoved:
                onPawnMoved?.Play();
                break;
            case BoardEventType.PawnCaptured:
                onPawnCaptured?.Play();
                break;
            case BoardEventType.PawnPromoted:
                onPawnPromoted?.Play();
                break;
            case BoardEventType.GameOver:
                onGameOver?.Play();
                break;
        }
    }
}