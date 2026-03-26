using UnityEngine;
using YokaiNoMori.Enumeration;

public interface ITurnService : IDisposableService 
{
    void SwitchTurn();  

    ECampType GetCurrentTurn();
    void SetStartingPlayer(ECampType startingCamp);
}
