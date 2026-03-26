using UnityEngine;
using YokaiNoMori.Enumeration;

public class PawnRetrieveView : MonoBehaviour
{
    [SerializeField] private Transform anchorP1;
    [SerializeField] private Transform anchorP2;
    [SerializeField] private float slotSpacing = 1.2f;
    [SerializeField] private int maxPerRow = 2;

    public Vector3 GetReservePosition(int pIndex, ECampType camp)
    {
        Transform targetAnchor = (camp == ECampType.PLAYER_ONE) ? anchorP1 : anchorP2;
        int row = pIndex / maxPerRow;
        int col = pIndex % maxPerRow;

        float direction = (camp == ECampType.PLAYER_ONE) ? 1f : -1f;

        Vector3 offset = new Vector3(col * slotSpacing, 0, -row * slotSpacing * direction);
        return targetAnchor.position + offset;
    }
}