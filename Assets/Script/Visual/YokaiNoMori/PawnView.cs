// =============================================================================
// PawnView.cs
// =============================================================================
// Représentation visuelle d'un pion dans la scène.
// Reçoit les événements de déplacement/capture via BoardView et utilise un
// MyFeedbackPlayer contenant un MyFeedbackTweenPosition pour animer le mouvement.
//
// ARCHITECTURE :
// BoardView reçoit le BoardEvent → appelle PawnView.MoveTo(pos, duration, curve)
// → PawnView configure le feedback avec SetTarget() → feedback.Play() lance le tween
// =============================================================================

using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class PawnView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Feedbacks")]
    [SerializeField] private MyFeedbackPlayer moveFeedback;

    private const float sliceSize = 0.8f;
    private IPawn model;

    /// <summary>
    /// Initialise le visuel du pion avec son sprite et sa référence logique.
    /// </summary>
    public void Setup(Sprite sprite, IPawn pawn)
    {
        model = pawn;

        if (spriteRenderer == null)
            Debug.LogError("SpriteRenderer non assigné sur PawnView.");

        spriteRenderer.sprite = sprite;
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.size = new Vector2(sliceSize, sliceSize);
    }

    /// <summary>
    /// Déplace le pion vers une position avec animation tweenée.
    /// Appelé par BoardView lors d'un BoardEvent.PawnMoved.
    /// </summary>
    /// <param name="targetPos">Position monde de la destination.</param>
    /// <param name="duration">Durée de l'animation (depuis YKNMGameSettings via BoardEvent).</param>
    /// <param name="tweenType">Courbe de tween (depuis YKNMGameSettings via BoardEvent).</param>
    public void MoveTo(Vector3 targetPos, float duration, Tools.MyTween.TweenType tweenType)
    {
        if (moveFeedback != null)
        {
            // Configure le feedback tween avec les paramètres de l'event
            var tweenFeedback = GetTweenFeedback();
            if (tweenFeedback != null)
            {
                tweenFeedback.SetTarget(targetPos, duration, tweenType);
                moveFeedback.Play();
                return;
            }
        }

        // Fallback sans feedback : déplacement immédiat
        transform.position = targetPos;
    }

    /// <summary>
    /// Déplace le pion vers la réserve du capteur avec animation.
    /// </summary>
    public void MoveToReserve(Vector3 targetPos, ECampType catcherCamp, float duration, Tools.MyTween.TweenType tweenType)
    {
        // Rotation pour faire face au bon joueur
        float rotY = catcherCamp == ECampType.PLAYER_ONE ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, rotY, 0);

        MoveTo(targetPos, duration, tweenType);
    }

    /// <summary>Retourne la référence logique du pion (IPawn).</summary>
    public IPawn GetModel() => model;

    /// <summary>Met à jour le sprite (utilisé lors de la promotion ou capture).</summary>
    public void UpdateSprite(Sprite newSprite)
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = newSprite;
    }

    /// <summary>
    /// Cherche le premier MyFeedbackTweenPosition dans le MyFeedbackPlayer.
    /// </summary>
    private MyFeedbackTweenPosition GetTweenFeedback()
    {
        if (moveFeedback == null) return null;

        foreach (var fb in moveFeedback.Feedbacks)
        {
            if (fb is MyFeedbackTweenPosition tweenFb)
                return tweenFb;
        }
        return null;
    }
}