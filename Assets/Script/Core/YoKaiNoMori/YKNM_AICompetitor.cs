// =============================================================================
// YKNM_AICompetitor.cs
// =============================================================================
// Composant Unity (MonoBehaviour) représentant le joueur IA dans la scène.
// Implémente ICompetitor pour s'intégrer dans le système de jeu existant.
//
// ARCHITECTURE GÉNÉRALE :
// Ce script agit comme chef d'orchestre entre le thread principal Unity et le
// Worker Thread qui exécute les calculs Minimax. Il ne fait AUCUN calcul IA
// lui-même — son rôle est de :
//   1. Convertir l'état du jeu (objets Unity managés) en données natives (Job)
//   2. Planifier le Job Burst sur un Worker Thread (Schedule)
//   3. Surveiller l'avancement via Update() sans bloquer le thread principal
//   4. Récupérer le résultat et l'exécuter via gameManager.DoAction()
//
// ITERATIVE DEEPENING :
// Au lieu de lancer directement à profondeur max, l'IA explore successivement
// les profondeurs 1, 2, 3, ... tant que le temps le permet.
// Avantages :
//   - Le meilleur coup de la dernière profondeur COMPLÈTE est toujours disponible
//   - Les Killer Moves des profondeurs précédentes améliorent le tri des suivantes
//   - En cas de timeout, on joue le résultat de la dernière profondeur complète
//     sans bloquer le thread principal (le Job continue en arrière-plan)
//
// GESTION MÉMOIRE NATIVE :
// Les structures Unity.Collections (NativeArray, NativeList, NativeHashMap)
// doivent être allouées et libérées manuellement (pas de GC).
// Deux catégories de mémoire :
//   - Persistante (Allocator.Persistent) : survit entre les tours
//     (transpositionTable, zobristTable, killerMoves, jobResult, logBuffer)
//   - Par itération (Allocator.Persistent, libérée après Complete()) :
//     initialBoard, initialReserveP1, initialReserveP2
//
// GESTION DU TIMEOUT :
// Quand le temps est écoulé et que le Job tourne encore, on N'appelle PAS
// Complete() — on joue directement le résultat de la dernière profondeur
// complète (sauvegardée dans _lastCompletedDepthMove). Le Job est complété
// proprement au début du tour suivant dans StartTurn(), pendant que la caméra
// tourne et que les animations jouent — ce Complete() est alors invisible.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using YokaiNoMori.Enumeration;
using YokaiNoMori.Interface;

public class YKNM_AICompetitor : MonoBehaviour, ICompetitor
{
    #region Champs — Iterative Deepening

    /// <summary>Profondeur explorée par le Job actuellement en cours ou venant de se terminer.</summary>
    private int currentSearchDepth = 1;

    /// <summary>
    /// Temps de réflexion maximum par tour (en secondes).
    /// Calculé depuis timerForAI avec une marge de sécurité de 0.8s pour garantir
    /// que DoAction() est appelé avant l'expiration du timer du tournoi.
    /// </summary>
    private float maxThinkingTime = 5f;

    /// <summary>Time.time au moment où StartTurn() a été appelé. Sert à mesurer le temps écoulé.</summary>
    private float turnStartTime;

    #endregion

    #region Champs — Références et état du Job

    /// <summary>Référence vers le GameManager fourni lors de Init(). Utilisé pour lire l'état et jouer le coup.</summary>
    private IGameManager gameManager;

    /// <summary>Camp de cette IA (PLAYER_ONE ou PLAYER_TWO), assigné par Init().</summary>
    private ECampType camp;

    /// <summary>Handle Unity permettant de vérifier si le Job est terminé et de le synchroniser.</summary>
    private JobHandle jobHandle;

    /// <summary>True si un Job Minimax est en cours d'exécution sur le Worker Thread.</summary>
    private bool isThinking = false;

    /// <summary>
    /// Verrou d'une frame après WaitAndExecute() pour éviter que StartTurn()
    /// soit rappelé pendant que la coroutine s'exécute encore.
    /// </summary>
    private bool frameDelayActive = false;

    #endregion

    #region Champs — Mémoire native persistante (communication avec le Job)

    /// <summary>
    /// Tableau de taille 2 partagé avec le Job pour recevoir le résultat.
    /// [0] : Réservé — non utilisé (conservé pour compatibilité future).
    /// [1] : Meilleur coup calculé par l'itération en cours (écrit par le Job dans Execute()).
    /// Note : en cas de timeout, on lit _lastCompletedDepthMove plutôt que jobResult
    /// car le Job tourne encore et Unity interdit la lecture concurrente.
    /// </summary>
    private NativeArray<JobYokaiMove> jobResult;

    /// <summary>Meilleur coup retenu pour être joué à la fin du tour (copié depuis jobResult).</summary>
    private JobYokaiMove bestMoveFoundThisTurn;

    /// <summary>
    /// Sauvegarde C# du meilleur coup de la dernière profondeur COMPLÈTE.
    /// Utilisée en cas de timeout à la place de jobResult (qui est inaccessible
    /// tant que le Job tourne). Mise à jour à chaque fin de profondeur (CAS 1).
    /// </summary>
    private JobYokaiMove _lastCompletedDepthMove;

    /// <summary>
    /// Table de transposition persistante entre les itérations du même tour.
    /// Clé : hash Zobrist 64-bit. Valeur : entrée TTEntry (score + flag + profondeur).
    /// Vidée en début de chaque nouveau tour car les évaluations deviennent obsolètes.
    /// Taille : 50 000 entrées (compromis mémoire/collisions pour un plateau 3x4).
    /// </summary>
    private NativeHashMap<ulong, TTEntry> transpositionTable;

    /// <summary>
    /// Clés pseudo-aléatoires 64-bit pour le hachage de Zobrist.
    /// Générées une seule fois avec une seed fixe (42) pour reproductibilité.
    /// Taille : (3×4×5×2) + 3 = 123 entrées.
    /// </summary>
    private NativeArray<ulong> zobristTable;

    // Constantes dupliquées depuis YKNMMinimaxJob pour le calcul de taille de zobristTable
    private const int BoardWidth = 3;
    private const int BoardHeight = 4;
    private const int NumPieceTypes = 5;
    private const int NumPlayers = 2;

    #endregion

    #region Champs — Mémoire native par itération (données d'entrée du Job)

    /// <summary>
    /// Snapshots du plateau et des réserves fournis au Job avant chaque Schedule().
    /// Alloués en Persistent pour survivre aux transitions de frames sans déclencher
    /// l'avertissement Unity "NativeArray created with TempJob is too old".
    /// Libérés immédiatement après jobHandle.Complete() via CleanupCurrentJobMemory().
    /// </summary>
    private NativeList<JobPawnState> initialBoard;
    private NativeList<EPawnType> initialReserveP1;
    private NativeList<EPawnType> initialReserveP2;

    #endregion

    #region Champs — Killer Moves

    /// <summary>
    /// Tableau des Killer Moves partagé avec le Job.
    /// Layout : [profondeur * 2] = killer principal, [profondeur * 2 + 1] = killer secondaire.
    /// Taille : MaxDepthForKillers * 2 = 32 slots.
    /// Réinitialisé en début de chaque tour car les positions changent.
    /// Persistant entre les itérations du même tour pour accumuler les coupures.
    /// </summary>
    private NativeArray<JobYokaiMove> killerMoves;
    private const int MaxDepthForKillers = 16;

    #endregion

    #region Champs — Aspiration Windows

    /// <summary>
    /// Score du meilleur coup retenu à la fin de la dernière profondeur complète.
    /// Transmis au prochain Job comme centre de la fenêtre d'aspiration [score-50, score+50].
    /// Réinitialisé à 0 en début de chaque nouveau tour.
    /// </summary>
    private int lastDepthScore = 0;

    #endregion

    #region Champs — Logs de recherche

    /// <summary>
    /// Buffer natif dans lequel le Job écrit ses logs de recherche (nœuds racine et internes).
    /// Partagé avec le Job via LogBuffer. Vidé après lecture dans Update().
    /// Capacité max : 500 entrées par itération.
    /// </summary>
    private NativeList<SearchLog> logBuffer;
    private const int MaxLogBuffer = 500;

    #endregion

    #region Champs — Réserve ICompetitor

    /// <summary>Réserve locale de pions capturés (gérée par le GameManager via AddToReserve/RemoveFromReserve).</summary>
    private List<IPawn> reserve = new List<IPawn>();

    #endregion

    // =========================================================================
    #region Initialisation — Init / GenerateZobristKeys
    // =========================================================================

    /// <summary>
    /// Appelé par le GameManager avant le début de la partie pour initialiser l'IA.
    /// Alloue toute la mémoire native persistante nécessaire au système de Jobs.
    /// </summary>
    /// <param name="igameManager">Référence au GameManager (lecture état + DoAction).</param>
    /// <param name="timerForAI">Temps de réflexion alloué par le système de tournoi (en secondes).</param>
    /// <param name="currentCamp">Camp assigné à cette IA pour la partie.</param>
    public void Init(IGameManager igameManager, float timerForAI, ECampType currentCamp)
    {
        this.gameManager = igameManager;
        this.camp = currentCamp;
        this.maxThinkingTime = Mathf.Max(0.5f, timerForAI - 0.5f);

        if (!logBuffer.IsCreated)
            logBuffer = new NativeList<SearchLog>(MaxLogBuffer, Allocator.Persistent);

        if (!killerMoves.IsCreated)
            killerMoves = new NativeArray<JobYokaiMove>(MaxDepthForKillers * 2, Allocator.Persistent);

        if (!jobResult.IsCreated)
            jobResult = new NativeArray<JobYokaiMove>(2, Allocator.Persistent);

        if (!transpositionTable.IsCreated)
            transpositionTable = new NativeHashMap<ulong, TTEntry>(50000, Allocator.Persistent);

        if (!zobristTable.IsCreated)
        {
            int totalCombinations = (BoardWidth * BoardHeight * NumPieceTypes * NumPlayers) + 3;
            zobristTable = new NativeArray<ulong>(totalCombinations, Allocator.Persistent);
            GenerateZobristKeys();
        }

        Debug.Log($"[IA - {camp}] Initialisée (Burst + Zobrist). Temps max : {this.maxThinkingTime}s");
    }

    /// <summary>
    /// Génère les clés Zobrist pseudo-aléatoires avec une seed fixe.
    /// La seed fixe (42) garantit la reproductibilité sans impacter la qualité du hachage.
    /// </summary>
    private void GenerateZobristKeys()
    {
        System.Random rand = new System.Random(42);
        for (int i = 0; i < zobristTable.Length; i++)
        {
            byte[] buffer = new byte[8];
            rand.NextBytes(buffer);
            zobristTable[i] = System.BitConverter.ToUInt64(buffer, 0);
        }
    }

    #endregion

    // =========================================================================
    #region Gestion mémoire — CleanupCurrentJobMemory / OnDestroy
    // =========================================================================

    /// <summary>
    /// Libère les listes natives allouées pour la dernière itération du Job.
    /// Appelée immédiatement après jobHandle.Complete() pour éviter les fuites mémoire.
    /// </summary>
    private void CleanupCurrentJobMemory()
    {
        if (initialBoard.IsCreated) initialBoard.Dispose();
        if (initialReserveP1.IsCreated) initialReserveP1.Dispose();
        if (initialReserveP2.IsCreated) initialReserveP2.Dispose();
    }

    /// <summary>
    /// Appelé par Unity lors de la destruction de l'objet (fin de partie, changement de scène).
    /// Libère TOUTE la mémoire native persistante pour éviter les fuites mémoire.
    /// Si un Job tourne encore, on le force à terminer avant de libérer sa mémoire.
    /// </summary>
    private void OnDestroy()
    {
        if (isThinking)
        {
            jobHandle.Complete();
            CleanupCurrentJobMemory();
            isThinking = false;
        }

        if (logBuffer.IsCreated) logBuffer.Dispose();
        if (killerMoves.IsCreated) killerMoves.Dispose();
        if (jobResult.IsCreated) jobResult.Dispose();
        if (transpositionTable.IsCreated) transpositionTable.Dispose();
        if (zobristTable.IsCreated) zobristTable.Dispose();
    }

    #endregion

    // =========================================================================
    #region Interface ICompetitor — StartTurn
    // =========================================================================

    /// <summary>
    /// Appelé par le GameManager quand c'est le tour de cette IA.
    /// Si un Job du tour précédent tourne encore en arrière-plan (timeout),
    /// on le complète ici — pendant les animations adverses, ce Complete() est invisible.
    /// Lance ensuite la première itération de l'Iterative Deepening (profondeur 1).
    /// </summary>
    public void StartTurn()
    {
        // Toujours compléter le Job précédent s'il tourne encore,
        // que isThinking soit true ou false (cas du timeout où isThinking=false mais Job actif)
        if (!jobHandle.IsCompleted)
        {
            jobHandle.Complete();
            CleanupCurrentJobMemory();
            isThinking = false;
        }
        else if (isThinking)
        {
            // isThinking=true mais Job déjà terminé (rare) — on nettoie quand même
            jobHandle.Complete();
            CleanupCurrentJobMemory();
            isThinking = false;
        }

        if (frameDelayActive) return;

        lastDepthScore = 0;
        _lastCompletedDepthMove = default;
        _lastCompletedDepthMove.IsValid = false;

        for (int i = 0; i < killerMoves.Length; i++) killerMoves[i] = default;

        turnStartTime = Time.time;
        currentSearchDepth = 1;
        bestMoveFoundThisTurn = default;
        bestMoveFoundThisTurn.IsValid = false;

        // Maintenant sûr d'écrire dans jobResult car le Job est complété
        jobResult[0] = default;
        jobResult[1] = default;
        transpositionTable.Clear();

        LaunchSearchJob(currentSearchDepth);
    }

    #endregion

    // =========================================================================
    #region Planification du Job — LaunchSearchJob
    // =========================================================================

    /// <summary>
    /// Convertit l'état Unity managé en données natives et planifie le Job Minimax
    /// à la profondeur cible sur le Worker Thread.
    /// Les NativeList sont allouées en Persistent pour survivre aux transitions de frames.
    /// </summary>
    private void LaunchSearchJob(int targetDepth)
    {
        var boardPawns = gameManager.GetAllPawn();
        if (boardPawns == null || boardPawns.Count == 0) return;

        // Conversion des pions du plateau en données natives
        initialBoard = new NativeList<JobPawnState>(boardPawns.Count, Allocator.Persistent);
        foreach (var pawn in boardPawns)
            initialBoard.Add(new JobPawnState
            {
                Position = pawn.GetCurrentPosition(),
                Type = pawn.GetPawnType(),
                Owner = pawn.GetCurrentOwner().GetCamp()
            });

        // Conversion des réserves
        var p1Reserve = gameManager.GetReservePawnsByPlayer(ECampType.PLAYER_ONE);
        initialReserveP1 = new NativeList<EPawnType>(Mathf.Max(1, p1Reserve.Count), Allocator.Persistent);
        for (int i = 0; i < p1Reserve.Count; i++) initialReserveP1.Add(p1Reserve[i].GetPawnType());

        var p2Reserve = gameManager.GetReservePawnsByPlayer(ECampType.PLAYER_TWO);
        initialReserveP2 = new NativeList<EPawnType>(Mathf.Max(1, p2Reserve.Count), Allocator.Persistent);
        for (int i = 0; i < p2Reserve.Count; i++) initialReserveP2.Add(p2Reserve[i].GetPawnType());

        YKNMMinimaxJob minimaxJob = new YKNMMinimaxJob
        {
            AICamp = this.camp,
            MaxDepth = targetDepth,
            InitialBoard = this.initialBoard,
            InitialReserveP1 = this.initialReserveP1,
            InitialReserveP2 = this.initialReserveP2,
            BestMoveResult = this.jobResult,
            TranspositionTable = this.transpositionTable,
            ZobristTable = this.zobristTable,
            KillerMoves = this.killerMoves,
            PreviousScore = this.lastDepthScore,
            UseAspirationWindow = (currentSearchDepth > 1),
            LogBuffer = logBuffer.IsCreated ? this.logBuffer : default
        };

        isThinking = true;
        jobHandle = minimaxJob.Schedule();
        Debug.Log($"[IA - {camp}] Job lancé à profondeur {targetDepth}...");
    }

    #endregion

    // =========================================================================
    #region Surveillance du Job — Update
    // =========================================================================

    /// <summary>
    /// Vérifie à chaque frame si le Job a terminé ou si le temps imparti est écoulé.
    /// Non bloquant : si le Job tourne encore et que le temps est disponible,
    /// on rend la main à Unity sans rien faire.
    ///
    /// CAS 1 — Job terminé dans les temps (jobHandle.IsCompleted) :
    ///   On lit le résultat, on sauvegarde _lastCompletedDepthMove, et on lance
    ///   la profondeur suivante si le temps le permet (Iterative Deepening).
    ///
    /// CAS 2 — Timeout (temps écoulé, Job encore en cours) :
    ///   On N'appelle PAS Complete() pour éviter de bloquer le thread principal.
    ///   On joue _lastCompletedDepthMove (dernière profondeur complète disponible).
    ///   Le Job sera complété proprement au début du tour suivant dans StartTurn().
    /// </summary>
    private void Update()
    {
        if (!isThinking) return;

        float timeElapsed = Time.time - turnStartTime;

        // Pré-signalement au scheduler Unity pour accélérer la fin du Job
        if (timeElapsed >= maxThinkingTime * 0.90f && !jobHandle.IsCompleted)
            JobHandle.ScheduleBatchedJobs();

        if (jobHandle.IsCompleted) // CAS 1 : Job terminé naturellement
        {
            jobHandle.Complete();
            CleanupCurrentJobMemory();
            isThinking = false;

            // Affichage des logs de recherche (nœuds racine et internes)
            //for (int i = 0; i < logBuffer.Length; i++)
            //{
            //    SearchLog log = logBuffer[i];
            //    string moveLabel = log.BestMove.IsValid
            //        ? (log.BestMove.ActionType == EActionType.PARACHUTE
            //            ? $"PARACHUTE {log.BestMove.Pawn.Type} → {log.BestMove.Destination}"
            //            : $"MOVE {log.BestMove.Pawn.Type} {log.BestMove.SourcePosition} → {log.BestMove.Destination}")
            //        : "aucun";
            //    string prefix = log.IsRootNode ? "🌳 ROOT" : "🔍 NODE";
            //    Debug.Log($"{prefix} | depth={log.Depth} | score={log.Score,7} | " +
            //              $"alpha={log.Alpha,7} | beta={log.Beta,7} | " +
            //              $"MAX={log.IsMaximizing} | bestMove={moveLabel}");
            //}
            logBuffer.Clear();

            // Sauvegarde du résultat — _lastCompletedDepthMove est accessible même en cas de timeout
            if (jobResult[1].IsValid)
            {
                bestMoveFoundThisTurn = jobResult[1];
                lastDepthScore = jobResult[1].HeuristicScore;
                _lastCompletedDepthMove = bestMoveFoundThisTurn;
                Debug.Log($"[IA - {camp}] Profondeur {currentSearchDepth} | {timeElapsed:F2}s | Score : {lastDepthScore}");
            }

            // Iterative Deepening : passe à la profondeur suivante si le temps le permet
            if (timeElapsed < maxThinkingTime * 0.85f && currentSearchDepth < 30)
            {
                currentSearchDepth++;
                LaunchSearchJob(currentSearchDepth);
            }
            else
            {
                StartCoroutine(WaitAndExecute(bestMoveFoundThisTurn));
            }
        }
        else if (timeElapsed >= maxThinkingTime) // CAS 2 : Timeout — Job encore en cours
        {
            // On ne bloque PAS le thread principal — le Job continue en arrière-plan
            // et sera complété dans StartTurn() au tour suivant.
            isThinking = false;

            // _lastCompletedDepthMove est une variable C# normale, accessible sans Complete()
            if (_lastCompletedDepthMove.IsValid)
                bestMoveFoundThisTurn = _lastCompletedDepthMove;

            Debug.LogWarning($"[IA - {camp}] Timeout | Profondeur : {currentSearchDepth} | {timeElapsed:F2}s | Coup sauvegardé utilisé.");
            StartCoroutine(WaitAndExecute(bestMoveFoundThisTurn));
        }
    }

    /// <summary>
    /// Attend la fin de frame avant d'exécuter le coup choisi.
    /// Ce délai stabilise l'état Unity après Complete() et évite des conflits
    /// avec d'autres systèmes (BoardView, EventBus).
    /// </summary>
    private IEnumerator WaitAndExecute(JobYokaiMove move)
    {
        frameDelayActive = true;
        yield return new WaitForEndOfFrame();
        frameDelayActive = false;
        ExecuteBestMove(move);
    }

    #endregion

    // =========================================================================
    #region Exécution du coup — ExecuteBestMove / ExecuteFallbackMove / FindRealPawnFromJob
    // =========================================================================

    /// <summary>
    /// Traduit le JobYokaiMove (struct native) en appel DoAction sur le GameManager.
    /// Retrouve le vrai IPawn (objet Unity managé) correspondant aux coordonnées du coup.
    /// Si aucun coup valide n'a été trouvé, exécute un coup de secours pour débloquer le tour.
    /// </summary>
    private void ExecuteBestMove(JobYokaiMove move)
    {
        if (!move.IsValid)
        {
            Debug.LogWarning($"[IA - {camp}] Aucun coup valide — coup de secours.");
            ExecuteFallbackMove();
            return;
        }

        IPawn realPawn = FindRealPawnFromJob(move);
        if (realPawn != null)
            gameManager.DoAction(realPawn, move.Destination, move.ActionType);
        else
            Debug.LogError($"[IA - {camp}] Pion introuvable pour le coup calculé.");
    }

    /// <summary>
    /// Coup de secours joué si le Minimax n'a produit aucun résultat valide.
    /// Utilise uniquement IGameManager — aucun cast vers YKNMManager,
    /// garantissant la compatibilité avec le GameManager du professeur au tournoi.
    /// Tente d'abord un déplacement légal, puis un parachutage depuis la réserve.
    /// </summary>
    private void ExecuteFallbackMove()
    {
        var boardPawns = gameManager.GetPawnsOnBoard(camp);
        foreach (var pawn in boardPawns)
        {
            Vector2Int pos = pawn.GetCurrentPosition();
            foreach (var dir in pawn.GetDirections())
            {
                Vector2Int dest = pos + dir;
                if (dest.x < 0 || dest.x >= 3 || dest.y < 0 || dest.y >= 4) continue;

                bool blocked = false;
                foreach (var ally in boardPawns)
                    if (ally.GetCurrentPosition() == dest) { blocked = true; break; }

                if (!blocked) { gameManager.DoAction(pawn, dest, EActionType.MOVE); return; }
            }
        }

        if (reserve.Count > 0)
            foreach (var bCase in gameManager.GetAllBoardCase())
                if (!bCase.IsBusy())
                {
                    gameManager.DoAction(reserve[0], bCase.GetPosition(), EActionType.PARACHUTE);
                    return;
                }
    }

    /// <summary>
    /// Retrouve le vrai IPawn (objet Unity managé) correspondant au coup calculé par le Job.
    /// Pour PARACHUTE : cherche par type dans la réserve du camp.
    /// Pour MOVE      : cherche par position source parmi les pions sur le plateau.
    /// </summary>
    private IPawn FindRealPawnFromJob(JobYokaiMove move)
    {
        if (move.ActionType == EActionType.PARACHUTE)
            return gameManager.GetReservePawnsByPlayer(camp)
                .Find(p => p.GetPawnType() == move.Pawn.Type);

        return gameManager.GetAllPawn()
            .Find(p => p.GetCurrentPosition() == move.SourcePosition
                    && p.GetCurrentOwner().GetCamp() == camp);
    }

    #endregion

    // =========================================================================
    #region Interface ICompetitor — Méthodes requises
    // =========================================================================

    public void StopTurn() { } // Le Job s'arrête via le timeout dans Update() ou StartTurn()
    public void GetDatas() { } // Non utilisé : l'état est lu directement dans LaunchSearchJob()

    public string GetName() => "YokaiAI_Burst_Zobrist";
    public ECampType GetCamp() => camp;
    public List<IPawn> GetReserve() => reserve;

    /// <summary>Appelé par le GameManager quand cette IA capture une pièce adverse.</summary>
    public void AddToReserve(IPawn pawn) => reserve.Add(pawn);

    /// <summary>Appelé par le GameManager quand cette IA parachute une pièce depuis sa réserve.</summary>
    public void RemoveFromReserve(IPawn pawn) => reserve.Remove(pawn);

    #endregion
}