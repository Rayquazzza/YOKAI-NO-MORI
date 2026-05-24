// =============================================================================
// GameSetup.cs
// =============================================================================
// MonoBehaviour responsable du bootstrapping de la partie.
// Crée et enregistre tous les services, configure le mode de jeu,
// et instancie les joueurs (humains ou IA) selon la configuration Inspector.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public enum GameModeType
{
    HumanVsHuman,
    HumanVsAI,
    AIVsAI
}

public class GameSetup : MonoBehaviour
{
    [InspectorGroup("Game Configuration",true,53)]
    [Header("Game Mode")]
    [SerializeField] private GameModeType gameMode = GameModeType.HumanVsAI;


    [InspectorGroup("AI Configuration",true,28)]
    [Header("AI Settings")]
    [SerializeField] private YKNM_AICompetitor AIPrefabP1;
    [SerializeField] private YKNM_AICompetitor AIPrefabP2;
    [SerializeField] private float timerForAI = 5f;

    [InspectorGroup("Game Data",true,78)]
    [Header("Game Data")]
    [SerializeField] private PawnDataLibrary pawnDataLibrary;
    [SerializeField] private YKNMGameSettings gameSettings;

    private List<IDisposableService> services = new List<IDisposableService>();

    public GameModeType GameMode => gameMode;
    public float TimerForAI => timerForAI;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Le YKNMManager reçoit le gameSettings et ce MonoBehaviour comme proxy coroutine
        var serviceArray = new object[]
        {
            new BoardGridService(pawnDataLibrary),
            new GameStateService(),
            new TurnService(),
            new YKNMManager(pawnDataLibrary, gameSettings, this),
            new PlayersService(),
        };

        foreach (var service in serviceArray)
        {
            if (service is IDisposableService disposableService)
                this.services.Add(disposableService);
            else
                Debug.LogError($"Service {service.GetType().Name} does not implement IDisposableService.");
        }
    }

    private void Start()
    {
        foreach (var service in services)
            if (service != null) service.Init();
    }

    private void OnDestroy()
    {
        foreach (var service in services)
            if (service != null) service.Dispose();
    }

    public ICompetitor CreatePlayer1()
    {
        if (gameMode == GameModeType.AIVsAI && AIPrefabP1 != null)
            return Instantiate(AIPrefabP1);
        return new Player(ECampType.PLAYER_ONE);
    }

    public ICompetitor CreatePlayer2()
    {
        if ((gameMode == GameModeType.HumanVsAI || gameMode == GameModeType.AIVsAI) && AIPrefabP2 != null)
            return Instantiate(AIPrefabP2);
        return new Player(ECampType.PLAYER_TWO);
    }
}