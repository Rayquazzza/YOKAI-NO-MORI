using System;
using UnityEngine;
using YokaiNoMori.Enumeration;

public class GameManager : MonoBehaviour
{
    private IGridService grid;
    private IGameStateService gameState;
    private ITurnService turnService;
    private IPlayersService playersService;

    private void Start()
    {
        GetServices();
        Initialize();
    }

    private void GetServices()
    {
        gameState = GameServiceLocator.Get<IGameStateService>();
        grid = GameServiceLocator.Get<IGridService>();
        turnService = GameServiceLocator.Get<ITurnService>();
        playersService = GameServiceLocator.Get<IPlayersService>();
    }

    private void Initialize()
    {
        grid.InitializeGrid(3, 4);

        // Création des joueurs
        Player p1 = new Player(ECampType.PLAYER_ONE);
        Player p2 = new Player(ECampType.PLAYER_TWO);
        playersService.RegisterPlayers(p1, p2);

        grid.SpawnInitialPieces(p1, p2);

        turnService.SetStartingPlayer(ECampType.PLAYER_ONE);

        // Lancer le moteur
        Debug.Log("Le plateau est prêt !");
    }
}
