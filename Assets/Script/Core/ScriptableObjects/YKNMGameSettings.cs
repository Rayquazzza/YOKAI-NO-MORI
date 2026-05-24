// =============================================================================
// YKNMGameSettings.cs
// =============================================================================
// ScriptableObject contenant toutes les données de configuration du jeu.
// Centralise les durées d'animation et de gameplay pour que le back-end (YKNMManager)
// et le front-end (BoardView, PawnView) restent synchronisés SANS se parler directement.
//
// PRINCIPE DE COORDINATION SANS COUPLAGE :
// Le YKNMManager lit les durées ici, attend le temps nécessaire avant de continuer
// la logique, et passe la durée dans le BoardEvent pour que le visuel l'utilise.
// Exemple : moveDuration = 0.4s → Manager attend 0.4s, BoardView anime sur 0.4s.
// =============================================================================

using UnityEngine;
using Tools;

[CreateAssetMenu(fileName = "YKNMGameSettings", menuName = "YokaiNoMori/GameSettings")]
public class YKNMGameSettings : ScriptableObject
{
    [InspectorGroup("Durations and Delays", true, 22)]
    [Tooltip("Durée de l'animation de déplacement d'un pion sur le plateau.")]
    public float MoveDuration = 0.4f;

    [Tooltip("Durée de l'animation de capture (pion qui part vers la réserve).")]
    public float CaptureDuration = 0.3f;

    [Tooltip("Durée de l'animation de parachutage (pion qui apparaît sur le plateau).")]
    public float ParachuteDuration = 0.35f;

    [Tooltip("Durée de l'animation de promotion (Kodama → KodamaSamurai).")]
    public float PromotionDuration = 0.5f;

    [Tooltip("Délai supplémentaire entre le coup et le passage au tour suivant.")]
    public float PostActionDelay = 0.1f;

    [InspectorGroup("Caméra", true, 36)]
    [Tooltip("Durée de la rotation de caméra lors du changement de tour.")]
    public float CameraRotationDuration = 0.5f;

    [Tooltip("Rotation Z de la caméra pour le camp PLAYER_ONE.")]
    public float CameraRotationP1 = 0f;

    [Tooltip("Rotation Z de la caméra pour le camp PLAYER_TWO.")]
    public float CameraRotationP2 = 180f;

    [InspectorGroup("Courbes de Tween", true, 54)]
    [Tooltip("Courbe de tween pour le déplacement.")]
    public MyTween.TweenType MoveTweenType = MyTween.TweenType.EaseOutCubic;

    [Tooltip("Courbe de tween pour la capture.")]
    public MyTween.TweenType CaptureTweenType = MyTween.TweenType.EaseInOutQuadratic;

    [Tooltip("Courbe de tween pour le parachutage.")]
    public MyTween.TweenType ParachuteTweenType = MyTween.TweenType.EaseOutBounce;

    [Tooltip("Courbe de tween pour la rotation de caméra.")]
    public MyTween.TweenType CameraRotationTweenType = MyTween.TweenType.EaseInOutCubic;
}