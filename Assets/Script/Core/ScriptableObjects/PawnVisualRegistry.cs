using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Nécessaire pour FirstOrDefault
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

[CreateAssetMenu(fileName = "PawnVisualRegistry", menuName = "ScriptableObjects/PawnVisualRegistry", order = 1)]
public class PawnVisualRegistry : ScriptableObject
{
    public List<PawnVisualEntry> PawnVisualEntries = new List<PawnVisualEntry>();

    public Sprite GetPawnSprite(EPawnType pawnType)
    {
        var entry = PawnVisualEntries.FirstOrDefault(e => e.PawnType == pawnType);

        if (entry != null)
        {
            return entry.VisualSprite;
        }
        Debug.LogWarning($"[PawnVisualRegistry] Aucun sprite trouvé pour le type : {pawnType}");
        return null;
    }
}

[Serializable]
public class PawnVisualEntry
{
    public EPawnType PawnType;
    public Sprite VisualSprite;
}