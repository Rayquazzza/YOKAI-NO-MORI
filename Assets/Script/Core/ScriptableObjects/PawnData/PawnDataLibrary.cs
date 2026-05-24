using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;

[CreateAssetMenu(fileName = "PawnDataLibrary", menuName = "YokaiNoMori/PawnDataLibrary")]
public class PawnDataLibrary : ScriptableObject
{
    public List<PawnData> allPawnData;

    public PawnData GetByType(EPawnType type) => allPawnData.Find(d => d.pawnType == type);

    public List<Vector2Int> GetDirectionsForType(EPawnType type, ECampType camp)
    {
        var data = GetByType(type);

        if (data == null)
        {
            Debug.LogError($"PawnData introuvable pour le type : {type}");
            return new List<Vector2Int>();
        }

        return data.GetConvertDirs(camp);
    }
}