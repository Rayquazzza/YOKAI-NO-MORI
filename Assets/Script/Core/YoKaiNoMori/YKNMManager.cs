// =============================================================================
// YKNMManager.cs
// =============================================================================
// Moteur de jeu (back-end) pour Yokai no Mori. C'est l'arbitre central :
// il reçoit les demandes d'action (humain via BoardEvent ou IA via DoAction),
// valide les coups, met à jour l'état logique, et publie des événements pour
// que le front-end (BoardView) réagisse.
//
// SÉPARATION BACK/FRONT :
// Ce script ne contient AUCUNE référence à des objets visuels (Transform, Sprite,
// GameObject). La synchronisation avec les animations passe uniquement par :
//   1. Le YKNMGameSettings (ScriptableObject) qui définit les durées et courbes
//   2. Les BoardEvent et CameraEvent qui transportent ces durées vers le front-end
//   3. Des WaitForSeconds dans PostActionSequence qui font attendre le back-end
//
// COORDINATION TEMPORELLE (PostActionSequence) :
//   1. Attend la fin de l'animation du pion (animDuration)
//   2. Vérifie promotion et victoire
//   3. Attend le délai post-action (PostActionDelay)
//   4. Change de tour, publie TurnChanged et CameraEvent
//   5. Attend la rotation de caméra (CameraRotationDuration)
//   6. Notifie le prochain joueur (StartTurn pour l'IA)
// Ce séquencement garantit que l'IA ne commence à réfléchir qu'une fois la
// caméra en place, et que le Complete() du Job précédent (timeout) se termine
// pendant les étapes 1 à 5 — invisible pour le joueur.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;
using YokaiNoMori.Struct;

public class YKNMManager : IGameManager, IDisposableService, IEventListener<BoardEvent>
{
    #region Champs — État du jeu

    /// <summary>Dernière action effectuée, accessible via GetLastAction().</summary>
    private SAction lastAction;

    /// <summary>Références vers les services du jeu (plateau, tours, joueurs).</summary>
    private IGridService gridService;
    private ITurnService turnService;
    private IPlayersService playersService;

    /// <summary>
    /// Mémorise si un Koropokkuru est sur le trône adverse depuis le tour précédent.
    /// La victoire par trône nécessite de SURVIVRE un tour complet sur la ligne adverse.
    /// null = aucun roi sur le trône, ECampType = le camp dont le roi est en position.
    /// </summary>
    private ECampType? koropokkuruOnThronePlayer = null;

    /// <summary>Bibliothèque des données de pions (directions par type et par camp).</summary>
    private PawnDataLibrary pawnDataLibrary;

    /// <summary>
    /// Données de configuration : durées d'animation, courbes de tween, angles de caméra.
    /// Lues à chaque action pour coordonner le timing back-end ↔ front-end.
    /// </summary>
    private YKNMGameSettings gameSettings;

    /// <summary>
    /// Référence vers le MonoBehaviour hôte (GameSetup) pour pouvoir lancer des coroutines.
    /// Le YKNMManager n'est pas un MonoBehaviour, il délègue via ce proxy.
    /// </summary>
    private MonoBehaviour coroutineRunner;

    /// <summary>Compteur du nombre total de coups joués depuis le début de la partie.</summary>
    private int _totalMovesPlayed = 0;

    #endregion

    // =========================================================================
    #region Initialisation — Constructeur / Init / InitializeGame
    // =========================================================================

    /// <summary>
    /// Constructeur. Enregistre ce manager dans le ServiceLocator global.
    /// </summary>
    /// <param name="pawnDataLibrary">Directions et données des types de pions.</param>
    /// <param name="gameSettings">Durées d'animation, courbes de tween, angles de caméra.</param>
    /// <param name="coroutineRunner">MonoBehaviour hôte pour les coroutines (typiquement GameSetup).</param>
    public YKNMManager(PawnDataLibrary pawnDataLibrary, YKNMGameSettings gameSettings, MonoBehaviour coroutineRunner)
    {
        GameServiceLocator.Register<IGameManager>(this);
        this.pawnDataLibrary = pawnDataLibrary;
        this.gameSettings = gameSettings;
        this.coroutineRunner = coroutineRunner;
    }

    /// <summary>
    /// Initialisation différée : récupère les services, crée la partie, s'abonne aux événements.
    /// Appelé après que tous les services ont été créés et enregistrés dans GameSetup.Start().
    /// </summary>
    public void Init()
    {
        gridService = GameServiceLocator.Get<IGridService>();
        turnService = GameServiceLocator.Get<ITurnService>();
        playersService = GameServiceLocator.Get<IPlayersService>();

        this.EventStartListening<BoardEvent>();
        InitializeGame();
    }

    /// <summary>
    /// Configure la partie : crée la grille, les joueurs, place les pions initiaux.
    /// Publie un TurnChanged initial pour positionner la caméra sur P1,
    /// puis notifie le premier joueur (IA ou humain).
    /// </summary>
    private void InitializeGame()
    {
        gridService.InitializeGrid(3, 4);

        GameSetup setup = Object.FindFirstObjectByType<GameSetup>();
        if (setup == null) { Debug.LogError("GameSetup introuvable !"); return; }

        ICompetitor p1 = setup.CreatePlayer1();
        ICompetitor p2 = setup.CreatePlayer2();

        if (p1 is YKNM_AICompetitor aiP1) aiP1.Init(this, setup.TimerForAI, ECampType.PLAYER_ONE);
        if (p2 is YKNM_AICompetitor aiP2) aiP2.Init(this, setup.TimerForAI, ECampType.PLAYER_TWO);

        playersService.RegisterPlayers(p1, p2);
        gridService.SpawnInitialPieces(p1, p2);
        turnService.SetStartingPlayer(ECampType.PLAYER_ONE);

        // Positionne la caméra sur P1 dès le départ (sans animation, via CameraEventType.SnapTo)
        BoardEvent.Trigger(BoardEventType.TurnChanged, camp: ECampType.PLAYER_ONE);

        Debug.Log($"Partie configurée : {setup.GameMode}");
        NotifyCurrentPlayerTurn();
    }

    /// <summary>Désinscription du ServiceLocator et de l'EventBus.</summary>
    public void Dispose()
    {
        GameServiceLocator.Unregister<IGameManager>();
        this.EventStopListening<BoardEvent>();
    }

    #endregion

    // =========================================================================
    #region Réception des événements — OnEvent
    // =========================================================================

    /// <summary>
    /// Point d'entrée pour les actions du joueur humain.
    /// Le SelectionHandler publie un BoardEvent.ActionRequested quand le joueur
    /// clique sur une case cible, et ce callback l'exécute.
    /// Les IA appellent DoAction() directement — elles ne passent pas par cet event.
    /// </summary>
    public void OnEvent(BoardEvent e)
    {
        if (e.EventType == BoardEventType.ActionRequested)
            DoAction(e.Pawn, e.Destination, e.ActionType);
    }

    #endregion

    // =========================================================================
    #region Exécution d'une action — DoAction / PostActionSequence
    // =========================================================================

    /// <summary>
    /// Point d'entrée principal pour toute action de jeu (humain ou IA).
    /// Valide le coup, met à jour l'état logique, publie les événements visuels
    /// avec les durées lues depuis YKNMGameSettings, puis lance PostActionSequence.
    /// </summary>
    public void DoAction(IPawn pawnTarget, Vector2Int destination, EActionType actionType)
    {
        Vector2Int startPos = pawnTarget.GetCurrentPosition();
        IBoardCase targetCase = gridService.GetBoardCaseByPosition(destination);
        float animDuration = 0f;

        switch (actionType)
        {
            case EActionType.NONE:
                Debug.LogWarning("ActionType NONE reçu dans DoAction — aucune action effectuée.");
                return;

            case EActionType.PARACHUTE:
                if (targetCase.IsBusy()) return;

                pawnTarget.GetCurrentOwner().RemoveFromReserve(pawnTarget);
                targetCase.SetPawn(pawnTarget);
                ((BoardPiece)pawnTarget).SetPosition(destination, targetCase);

                animDuration = gameSettings.ParachuteDuration;
                BoardEvent.Trigger(BoardEventType.PawnMoved,
                    pawn: pawnTarget, destination: destination,
                    duration: animDuration, tweenType: gameSettings.ParachuteTweenType);
                break;

            case EActionType.MOVE:
                if (!GetValidMoves(pawnTarget).Contains(destination))
                {
                    Debug.LogWarning($"Mouvement invalide vers {destination}");
                    return;
                }

                IBoardCase oldCase = pawnTarget.GetCurrentBoardCase();

                if (targetCase.IsBusy())
                {
                    Capture(targetCase.GetPawnOnIt(), pawnTarget.GetCurrentOwner());
                    targetCase.SetPawn(null);
                }

                oldCase.SetPawn(null);
                targetCase.SetPawn(pawnTarget);
                ((BoardPiece)pawnTarget).SetPosition(destination, targetCase);

                animDuration = gameSettings.MoveDuration;
                BoardEvent.Trigger(BoardEventType.PawnMoved,
                    pawn: pawnTarget, destination: destination,
                    duration: animDuration, tweenType: gameSettings.MoveTweenType);
                break;
        }

        _totalMovesPlayed++;
        Debug.Log($"[YKNMManager] Coup #{_totalMovesPlayed} | {pawnTarget.GetCurrentOwner().GetCamp()} | {actionType} | {pawnTarget.GetPawnType()} → {destination}");

        lastAction.SetAction(pawnTarget.GetCurrentOwner().GetCamp(), pawnTarget.GetPawnType(),
                             actionType, startPos, destination, targetCase.GetPawnOnIt());

        coroutineRunner.StartCoroutine(PostActionSequence(pawnTarget, destination, animDuration));
    }

    /// <summary>
    /// Séquence temporelle post-action. Coordonne les animations et la logique de jeu :
    ///   1. Attend la fin de l'animation du pion
    ///   2. Vérifie promotion et victoire
    ///   3. Attend le délai post-action (pour que les feedbacks finissent)
    ///   4. Change de tour, publie TurnChanged et déclenche la rotation de caméra
    ///   5. Attend la fin de la rotation de caméra
    ///   6. Notifie le prochain joueur (StartTurn pour l'IA, rien pour l'humain)
    /// Note : le Complete() du Job précédent en timeout se termine pendant les étapes 1-5.
    /// </summary>
    private IEnumerator PostActionSequence(IPawn pawnTarget, Vector2Int destination, float animDuration)
    {
        yield return new WaitForSeconds(animDuration);

        CheckPromotion(pawnTarget, destination);
        CheckVictory();

        yield return new WaitForSeconds(gameSettings.PostActionDelay);

        turnService.SwitchTurn();
        ECampType nextCamp = turnService.GetCurrentTurn();
        BoardEvent.Trigger(BoardEventType.TurnChanged, camp: nextCamp);

        float camRotTarget = nextCamp == ECampType.PLAYER_ONE
            ? gameSettings.CameraRotationP1
            : gameSettings.CameraRotationP2;
        CameraEvent.Trigger(CameraEventType.RotateTo, camRotTarget,
            gameSettings.CameraRotationDuration, gameSettings.CameraRotationTweenType);

        yield return new WaitForSeconds(gameSettings.CameraRotationDuration);

        NotifyCurrentPlayerTurn();
    }

    /// <summary>
    /// Notifie le joueur dont c'est le tour.
    /// Pour une IA : appelle StartTurn() qui peut compléter le Job précédent en timeout.
    /// Pour un humain : rien — il interagit via SelectionHandler → BoardEvent.ActionRequested.
    /// </summary>
    private void NotifyCurrentPlayerTurn()
    {
        ECampType currentCamp = turnService.GetCurrentTurn();
        ICompetitor currentPlayer = playersService.GetPlayer(currentCamp);

        if (currentPlayer is YKNM_AICompetitor aiPlayer)
            aiPlayer.StartTurn();
    }

    #endregion

    // =========================================================================
    #region Capture
    // =========================================================================

    /// <summary>
    /// Gère la capture d'un pion adverse : change son propriétaire, le rétrograde
    /// si c'est un KodamaSamurai, l'ajoute à la réserve du capteur.
    /// Publie un BoardEvent.PawnCaptured avec la durée d'animation de capture.
    /// </summary>
    private void Capture(IPawn victim, ICompetitor catcher)
    {
        if (victim.GetPawnType() == EPawnType.Koropokkuru)
            koropokkuruOnThronePlayer = null;

        ((BoardPiece)victim).SetOwner(catcher);

        // Un KodamaSamurai ou Kodama capturé redevient Kodama dans la réserve adverse
        if (victim.GetPawnType() == EPawnType.KodamaSamurai || victim.GetPawnType() == EPawnType.Kodama)
        {
            List<Vector2Int> resetDirs = pawnDataLibrary.GetDirectionsForType(EPawnType.Kodama, catcher.GetCamp());
            ((BoardPiece)victim).Demote(resetDirs);
        }

        catcher.AddToReserve(victim);

        BoardEvent.Trigger(BoardEventType.PawnCaptured,
            pawn: victim, competitor: catcher,
            duration: gameSettings.CaptureDuration, tweenType: gameSettings.CaptureTweenType);
    }

    #endregion

    // =========================================================================
    #region Conditions de victoire — CheckVictory / CheckThroneVictory / DeclareWinner
    // =========================================================================

    /// <summary>
    /// Vérifie les deux conditions de victoire :
    ///   1. Capture du Koropokkuru adverse (présent dans la réserve d'un joueur)
    ///   2. Percée : Koropokkuru sur la ligne adverse survivant un tour complet
    /// </summary>
    private void CheckVictory()
    {
        foreach (ECampType camp in new[] { ECampType.PLAYER_ONE, ECampType.PLAYER_TWO })
        {
            if (playersService.GetPlayer(camp).GetReserve()
                .Exists(p => p.GetPawnType() == EPawnType.Koropokkuru))
            {
                DeclareWinner(camp);
                return;
            }
        }
        CheckThroneVictory();
    }

    /// <summary>
    /// Victoire par trône : le Koropokkuru doit atteindre la ligne adverse ET
    /// survivre un tour complet sans être capturé.
    /// koropokkuruOnThronePlayer mémorise le premier passage ;
    /// au tour suivant, si le roi est toujours là, la victoire est déclarée.
    /// </summary>
    private void CheckThroneVictory()
    {
        ECampType justPlayed = turnService.GetCurrentTurn();

        foreach (var pawn in gridService.GetAllPawn())
        {
            if (pawn.GetPawnType() != EPawnType.Koropokkuru) continue;
            if (pawn.GetCurrentOwner().GetCamp() != justPlayed) continue;

            Vector2Int pos = pawn.GetCurrentPosition();
            bool isOnEnemyThrone = (justPlayed == ECampType.PLAYER_ONE && pos.y == 3)
                                || (justPlayed == ECampType.PLAYER_TWO && pos.y == 0);

            if (isOnEnemyThrone)
            {
                if (koropokkuruOnThronePlayer == justPlayed)
                    DeclareWinner(justPlayed);
                else
                    koropokkuruOnThronePlayer = justPlayed;
                return;
            }
        }

        if (koropokkuruOnThronePlayer == justPlayed)
            koropokkuruOnThronePlayer = null;
    }

    /// <summary>Déclare le vainqueur et publie l'événement de fin de partie.</summary>
    private void DeclareWinner(ECampType winner)
    {
        BoardEvent.Trigger(BoardEventType.GameOver, camp: winner);
        Debug.Log($"FIN DE PARTIE : Le camp {winner} a gagné !");
    }

    #endregion

    // =========================================================================
    #region Promotion — CheckPromotion
    // =========================================================================

    /// <summary>
    /// Un Kodama qui atteint la dernière ligne adverse est automatiquement promu
    /// en KodamaSamurai (ses directions de déplacement changent).
    /// Publie un BoardEvent.PawnPromoted avec la durée d'animation de promotion.
    /// </summary>
    private void CheckPromotion(IPawn pawnTarget, Vector2Int destination)
    {
        if (pawnTarget.GetPawnType() != EPawnType.Kodama) return;

        ECampType camp = pawnTarget.GetCurrentOwner().GetCamp();
        bool canPromote = (camp == ECampType.PLAYER_ONE && destination.y == 3)
                       || (camp == ECampType.PLAYER_TWO && destination.y == 0);

        if (canPromote)
        {
            var newDirs = pawnDataLibrary.GetDirectionsForType(EPawnType.KodamaSamurai, camp);
            ((BoardPiece)pawnTarget).Promote(newDirs);
            BoardEvent.Trigger(BoardEventType.PawnPromoted,
                pawn: pawnTarget, duration: gameSettings.PromotionDuration);
            Debug.Log("Kodama promu en Kodama Samurai !");
        }
    }

    #endregion

    // =========================================================================
    #region Interface IGameManager — Méthodes de lecture
    // =========================================================================

    public List<IBoardCase> GetAllBoardCase() => gridService.GetAllBoardCase();
    public List<IPawn> GetAllPawn() => gridService.GetAllPawn();
    public SAction GetLastAction() => lastAction;

    public List<IPawn> GetPawnsOnBoard(ECampType campType)
        => gridService.GetAllPawn().FindAll(p => p.GetCurrentOwner().GetCamp() == campType);

    public List<IPawn> GetReservePawnsByPlayer(ECampType campType)
        => playersService.GetPlayer(campType).GetReserve();

    public EActionType GetActionType(IPawn pawn)
        => pawn.GetCurrentOwner().GetReserve().Contains(pawn) ? EActionType.PARACHUTE : EActionType.MOVE;

    /// <summary>
    /// Calcule tous les déplacements légaux d'un pion.
    /// Utilisé par le SelectionHandler (humain) et ExecuteFallbackMove (IA).
    /// </summary>
    public List<Vector2Int> GetValidMoves(IPawn pawn)
    {
        Vector2Int currentPos = pawn.GetCurrentPosition();
        List<Vector2Int> validPositions = new List<Vector2Int>();

        foreach (Vector2Int dir in pawn.GetDirections())
        {
            IBoardCase targetCase = gridService.GetBoardCaseByPosition(currentPos + dir);
            if (targetCase == null) continue;
            if (targetCase.IsBusy() && targetCase.GetPawnOnIt().GetCurrentOwner() == pawn.GetCurrentOwner()) continue;
            validPositions.Add(currentPos + dir);
        }

        return validPositions;
    }

    #endregion
}