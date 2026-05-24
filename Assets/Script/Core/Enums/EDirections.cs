using System;

namespace YokaiNoMori.Enumeration
{
    [Flags]
    public enum EDirections
    {
        None = 0,
        North = 1 << 0, // Haut      (1)
        NorthEast = 1 << 1, // Haut-Droit (2)
        East = 1 << 2, // Droite     (4)
        SouthEast = 1 << 3, // Bas-Droit  (8)
        South = 1 << 4, // Bas        (16)
        SouthWest = 1 << 5, // Bas-Gauche (32)
        West = 1 << 6, // Gauche     (64)
        NorthWest = 1 << 7  // Haut-Gauche(128)
    }
}