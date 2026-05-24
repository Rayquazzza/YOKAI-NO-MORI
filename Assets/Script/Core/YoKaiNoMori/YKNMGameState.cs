using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;

//public struct JobPawnState
//{
//    public Vector2Int Position;
//    public EPawnType Type;
//    public ECampType Owner;

//    public JobPawnState Clone() => new JobPawnState
//    {
//        Position = Position,
//        Type = Type,
//        Owner = Owner
//    };
//}

//public struct JobYokaiMove
//{
//    public JobPawnState Pawn;
//    public Vector2Int SourcePosition;
//    public Vector2Int Destination;
//    public EActionType ActionType;
//    public bool IsValid; // Ajouté pour fiabiliser la détection du coup par défaut
//}

public class YKNMGameState
{
    //public List<JobPawnState> Board = new List<JobPawnState>();
    //public List<JobPawnState> ReserveP1 = new List<JobPawnState>();
    //public List<JobPawnState> ReserveP2 = new List<JobPawnState>();
    //public ECampType CurrentPlayer;

    //public YKNMGameState Clone()
    //{
    //    var clone = new YKNMGameState { CurrentPlayer = CurrentPlayer };
    //    foreach (var p in Board) clone.Board.Add(p.Clone());
    //    foreach (var p in ReserveP1) clone.ReserveP1.Add(p.Clone());
    //    foreach (var p in ReserveP2) clone.ReserveP2.Add(p.Clone());
    //    return clone;
    //}

    //public List<JobPawnState> GetReserve(ECampType camp)
    //    => camp == ECampType.PLAYER_ONE ? ReserveP1 : ReserveP2;

    //public JobPawnState GetPawnAt(Vector2Int pos)
    //{
    //    // Find retourne default(JobPawnState) si rien n'est trouvé, ce qui est correct pour une struct
    //    return Board.Find(p => p.Position == pos);
    //}

    //public bool IsCellEmpty(Vector2Int pos)
    //{
    //    // On ne peut pas comparer une struct à null. On cherche si un pion existe à cette position.
    //    return !Board.Exists(p => p.Position == pos);
    //}
}