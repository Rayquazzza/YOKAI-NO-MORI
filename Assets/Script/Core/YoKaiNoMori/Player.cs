using System.Collections.Generic;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class Player : ICompetitor
{
    private ECampType camp;
    public Player(ECampType camp) => this.camp = camp;

    private IGameManager gameManager;

    private float timerForAI;

    private List<IPawn> reserve = new List<IPawn>();


    public void Init(IGameManager igameManager, float timerForAI, ECampType currentCamp)
    {
        gameManager = igameManager;
        this.timerForAI = timerForAI;
        camp = currentCamp;
    }

    public string GetName()
    {
        throw new System.NotImplementedException();
    }

    public ECampType GetCamp() => camp;


    public void GetDatas()
    {
        throw new System.NotImplementedException();
    }

    public void StartTurn()
    {
        throw new System.NotImplementedException();
    }

    public void StopTurn()
    {
        throw new System.NotImplementedException();
    }

    public void AddToReserve(IPawn pawn)
    {
        reserve.Add(pawn);
    }

    public void RemoveFromReserve(IPawn pawn)
    {
        reserve.Remove(pawn);
    }

    public List<IPawn> GetReserve()=> reserve;

}