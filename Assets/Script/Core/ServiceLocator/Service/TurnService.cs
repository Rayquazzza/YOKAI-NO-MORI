using UnityEngine;
using YokaiNoMori.Enumeration;

public class TurnService : ITurnService
{
    ECampType CurrentPlayer;

    public TurnService()
    {
        GameServiceLocator.Register<ITurnService>(this);
    }
    public void Dispose()
    {
       GameServiceLocator.Unregister<ITurnService>();
    }

    public ECampType GetCurrentTurn()
    {
        return CurrentPlayer;
    }

    public void Init()
    {
        
    }

    public void SetStartingPlayer(ECampType startingCamp)
    {
        CurrentPlayer = startingCamp;
        Debug.Log($"La partie commence ! Tour de : {CurrentPlayer}");
    }

    public void SwitchTurn()
    {
        CurrentPlayer = (CurrentPlayer == ECampType.PLAYER_ONE) ? ECampType.PLAYER_TWO : ECampType.PLAYER_ONE;
    }
}
