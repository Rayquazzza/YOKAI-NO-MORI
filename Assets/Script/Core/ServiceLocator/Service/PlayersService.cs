using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class PlayersService : IPlayersService
{
    private Dictionary<ECampType, ICompetitor> players = new();

    public PlayersService()
    {
        GameServiceLocator.Register<IPlayersService>(this);
    }

    public void Dispose()
    {
        GameServiceLocator.Unregister<IPlayersService>();   
    }
    public void Init()
    {

    }

    public void RegisterPlayers(ICompetitor p1, ICompetitor p2)
    {
        players[ECampType.PLAYER_ONE] = p1;
        players[ECampType.PLAYER_TWO] = p2;
    }

    public ICompetitor GetPlayer(ECampType camp) => players[camp];
}
