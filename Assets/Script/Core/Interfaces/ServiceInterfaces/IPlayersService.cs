using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public interface IPlayersService : IDisposableService
{
    void RegisterPlayers(ICompetitor p1, ICompetitor p2);
    ICompetitor GetPlayer(ECampType camp);
}
