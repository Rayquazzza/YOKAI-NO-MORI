using UnityEngine;
using YokaiNoMori.Enumeration;

public class PawnView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private const float sliceSize = 0.8f;

    public void Setup(Sprite sprite)
    {
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer is not assigned in BoardPieceView.");
        }

        spriteRenderer.sprite = sprite;

        spriteRenderer.drawMode = SpriteDrawMode.Sliced;

        spriteRenderer.size = new Vector2(sliceSize, sliceSize);
    }

    public void MoveToReserve(Vector3 targetPos, ECampType catcherCamp)
    {
        transform.position = targetPos;
        float rotY = (catcherCamp == ECampType.PLAYER_ONE) ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, rotY, 0);
    }

    public void MoveTo(Vector3 targetPos)
    {
        transform.position = targetPos;
    }
}
