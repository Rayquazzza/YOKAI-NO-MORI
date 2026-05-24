// =============================================================================
// YKNMMinimaxJob.cs
// =============================================================================
// Ce fichier contient l'intégralité du moteur de décision de l'IA pour
// Yokai no Mori. Il s'exécute entièrement sur un Worker Thread via le système
// de Jobs d'Unity, compilé en code natif optimisé par le compilateur Burst.
//
// ALGORITHMES UTILISÉS (du plus fondamental au plus avancé) :
//
// 1. MINIMAX avec ÉLAGAGE ALPHA-BETA
//    L'IA explore un arbre de tous les coups possibles jusqu'à une certaine
//    profondeur. À chaque nœud, elle alterne entre maximiser son avantage
//    (son tour) et minimiser celui de l'adversaire (tour adverse).
//    L'élagage Alpha-Beta abandonne les branches que l'adversaire n'accepterait
//    jamais, réduisant la complexité de O(b^d) à O(b^(d/2)).
//
// 2. TABLE DE TRANSPOSITION + HACHAGE DE ZOBRIST
//    Mémoïsation : chaque position déjà évaluée est mise en cache avec un
//    identifiant unique (hash Zobrist 64-bit). Si la même position est atteinte
//    par un chemin différent, le résultat en cache est réutilisé immédiatement.
//    Chaque entrée stocke aussi un Flag (Exact/Lower/Upper) et la profondeur
//    à laquelle le score a été calculé, pour éviter d'utiliser des scores trop
//    peu fiables.
//
// 3. MOVE ORDERING (Tri des coups)
//    Avant d'explorer les coups, on les trie par score heuristique décroissant :
//    captures > promotions > killer moves > parachutages > avancées > autres.
//    Un bon tri maximise les coupures Alpha-Beta (une branche coupée tôt
//    = des milliers de nœuds non explorés).
//
// 4. KILLER MOVES
//    Les coups qui ont causé une coupure Beta dans des nœuds similaires sont
//    mémorisés (2 par profondeur). Ils sont prioritisés au prochain nœud de
//    même profondeur, car ils ont de bonnes chances de couper à nouveau.
//
// 5. NULL MOVE PRUNING
//    Si l'adversaire ne peut pas battre notre score même en jouant deux fois
//    de suite (en "passant" notre tour), la branche est inutile à explorer.
//    Réduit l'arbre de manière très agressive sur les positions non critiques.
//
// 6. ASPIRATION WINDOWS
//    Au lieu de chercher dans [-∞, +∞], on cherche dans une fenêtre étroite
//    [score_précédent - 50, score_précédent + 50]. Si le résultat sort de la
//    fenêtre, on relance avec la fenêtre complète. Réduit le nombre de nœuds
//    explorés en profondeur successive (Iterative Deepening).
//
// 7. BITBOARDS
//    Le plateau est représenté par des masques de bits (ushort 16 bits, 1 bit
//    par case). Les vérifications d'occupation (case vide ? allié présent ?)
//    deviennent des opérations AND/OR sur des entiers : O(1) au lieu de O(n).
// =============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using YokaiNoMori.Enumeration;

// =============================================================================
// STRUCT : SearchLog
// =============================================================================
// Entrée de log écrite par le Job dans LogBuffer à chaque nœud important.
// Lue par YKNM_AICompetitor.Update() après Complete() pour afficher la trace
// de la recherche dans la console Unity.
// IsRootNode = true  → log de GetBestMove (nœud racine, choix final)
// IsRootNode = false → log de Minimax (nœud interne, 2 premières profondeurs)
// =============================================================================
public struct SearchLog
{
    public int Depth;
    public int Score;
    public int Alpha;
    public int Beta;
    public bool IsMaximizing;
    public JobYokaiMove BestMove;
    public bool IsRootNode;
}

// =============================================================================
// STRUCT : TTEntry
// =============================================================================
// Entrée de la table de transposition avec typage du nœud.
// Flag 0 = Exact   : score précis, la fenêtre [alpha,beta] n'a pas été coupée.
// Flag 1 = Lower   : coupure beta (fail-high), score réel >= cette valeur.
// Flag 2 = Upper   : coupure alpha (fail-low), score réel <= cette valeur.
// Depth  : profondeur à laquelle ce score a été calculé. On n'utilise une
//          entrée que si entry.Depth >= profondeur demandée.
// =============================================================================
public struct TTEntry
{
    public int Score;
    public int Depth;
    public byte Flag; // 0=Exact, 1=LowerBound, 2=UpperBound
}

// =============================================================================
// STRUCT : BitBoard
// =============================================================================
// Représente la présence/absence d'un type de pièce sur les 12 cases du plateau
// sous forme d'un entier 16 bits (ushort). Chaque bit correspond à une case.
//
// Layout des bits (plateau 3x4) :
//   Bit 0  = case (0,0)   Bit 1  = case (1,0)   Bit 2  = case (2,0)  ← ligne y=0 (bas, P2)
//   Bit 3  = case (0,1)   Bit 4  = case (1,1)   Bit 5  = case (2,1)
//   Bit 6  = case (0,2)   Bit 7  = case (1,2)   Bit 8  = case (2,2)
//   Bit 9  = case (0,3)   Bit 10 = case (1,3)   Bit 11 = case (2,3)  ← ligne y=3 (haut, P1)
// =============================================================================
public struct BitBoard
{
    /// <summary>Masque binaire 16-bit. Un bit à 1 = pièce présente sur cette case.</summary>
    public ushort Value;

    /// <summary>Met le bit à 1 (pose une pièce sur la case d'index donné).</summary>
    public void Set(int index) => Value |= (ushort)(1 << index);

    /// <summary>Met le bit à 0 (retire la pièce de la case d'index donné).</summary>
    public void Clear(int index) => Value &= (ushort)~(1 << index);

    /// <summary>Retourne true si une pièce occupe la case d'index donné.</summary>
    public bool IsSet(int index) => (Value & (1 << index)) != 0;

    /// <summary>
    /// Convertit des coordonnées 2D (x, y) en index de bit (0 à 11).
    /// Formule : index = x + (y * 3). Exemple : (1, 2) → 7.
    /// </summary>
    public static int PositionToIndex(Vector2Int pos) => pos.x + (pos.y * 3);

    /// <summary>Opération inverse de PositionToIndex : index → coordonnées 2D.</summary>
    public static Vector2Int IndexToPosition(int index) => new Vector2Int(index % 3, index / 3);
}

// =============================================================================
// STRUCT : BitBoardBoard
// =============================================================================
// Ensemble de 10 BitBoards représentant l'état complet du plateau :
// 5 types de pièces × 2 joueurs. Permet des vérifications d'occupation en O(1)
// via des opérations AND/OR/NOT sur des entiers, sans boucler sur les pions.
// SetPiece et ClearPiece maintiennent les BitBoards synchronisés avec Board.
// =============================================================================
public struct BitBoardBoard
{
    public BitBoard P1_Kodama, P1_Kitsune, P1_Tanuki, P1_KodamaSamurai, P1_Koropokkuru;
    public BitBoard P2_Kodama, P2_Kitsune, P2_Tanuki, P2_KodamaSamurai, P2_Koropokkuru;

    /// <summary>Masque de toutes les cases occupées par PLAYER_ONE.</summary>
    public ushort P1All => (ushort)(P1_Kodama.Value | P1_Kitsune.Value | P1_Tanuki.Value
                                  | P1_KodamaSamurai.Value | P1_Koropokkuru.Value);

    /// <summary>Masque de toutes les cases occupées par PLAYER_TWO.</summary>
    public ushort P2All => (ushort)(P2_Kodama.Value | P2_Kitsune.Value | P2_Tanuki.Value
                                  | P2_KodamaSamurai.Value | P2_Koropokkuru.Value);

    /// <summary>Masque de toutes les cases occupées (union des deux camps).</summary>
    public ushort AllPieces => (ushort)(P1All | P2All);

    /// <summary>
    /// Masque de toutes les cases VIDES.
    /// ~AllPieces inverse tous les bits ; & 0x0FFF masque les 12 bits utiles
    /// (les 4 bits hauts de ushort ne correspondent à aucune case du plateau).
    /// </summary>
    public ushort EmptyCells => (ushort)(~AllPieces & 0x0FFF);

    /// <summary>Allume le bit du BitBoard correspondant au type et au propriétaire donnés.</summary>
    public void SetPiece(EPawnType type, ECampType owner, int bitIndex)
    {
        if (owner == ECampType.PLAYER_ONE)
        {
            switch (type)
            {
                case EPawnType.Kodama: P1_Kodama.Set(bitIndex); break;
                case EPawnType.Kitsune: P1_Kitsune.Set(bitIndex); break;
                case EPawnType.Tanuki: P1_Tanuki.Set(bitIndex); break;
                case EPawnType.KodamaSamurai: P1_KodamaSamurai.Set(bitIndex); break;
                case EPawnType.Koropokkuru: P1_Koropokkuru.Set(bitIndex); break;
            }
        }
        else
        {
            switch (type)
            {
                case EPawnType.Kodama: P2_Kodama.Set(bitIndex); break;
                case EPawnType.Kitsune: P2_Kitsune.Set(bitIndex); break;
                case EPawnType.Tanuki: P2_Tanuki.Set(bitIndex); break;
                case EPawnType.KodamaSamurai: P2_KodamaSamurai.Set(bitIndex); break;
                case EPawnType.Koropokkuru: P2_Koropokkuru.Set(bitIndex); break;
            }
        }
    }

    /// <summary>Éteint le bit du BitBoard correspondant au type et au propriétaire donnés.</summary>
    public void ClearPiece(EPawnType type, ECampType owner, int bitIndex)
    {
        if (owner == ECampType.PLAYER_ONE)
        {
            switch (type)
            {
                case EPawnType.Kodama: P1_Kodama.Clear(bitIndex); break;
                case EPawnType.Kitsune: P1_Kitsune.Clear(bitIndex); break;
                case EPawnType.Tanuki: P1_Tanuki.Clear(bitIndex); break;
                case EPawnType.KodamaSamurai: P1_KodamaSamurai.Clear(bitIndex); break;
                case EPawnType.Koropokkuru: P1_Koropokkuru.Clear(bitIndex); break;
            }
        }
        else
        {
            switch (type)
            {
                case EPawnType.Kodama: P2_Kodama.Clear(bitIndex); break;
                case EPawnType.Kitsune: P2_Kitsune.Clear(bitIndex); break;
                case EPawnType.Tanuki: P2_Tanuki.Clear(bitIndex); break;
                case EPawnType.KodamaSamurai: P2_KodamaSamurai.Clear(bitIndex); break;
                case EPawnType.Koropokkuru: P2_Koropokkuru.Clear(bitIndex); break;
            }
        }
    }
}

// =============================================================================
// STRUCT : JobPawnState
// =============================================================================
// Représentation d'un pion compatible Burst/NativeArray.
// Contrairement à IPawn (interface Unity managée), ne contient que des types
// de valeur — pas de références vers des objets C#.
// =============================================================================
public struct JobPawnState
{
    /// <summary>Position sur le plateau (0,0) à (2,3).</summary>
    public Vector2Int Position;
    /// <summary>Type du pion (Kodama, Kitsune, Tanuki, KodamaSamurai, Koropokkuru).</summary>
    public EPawnType Type;
    /// <summary>Camp propriétaire du pion.</summary>
    public ECampType Owner;

    public JobPawnState Clone() => new JobPawnState { Position = Position, Type = Type, Owner = Owner };
}

// =============================================================================
// STRUCT : JobYokaiMove
// =============================================================================
// Décrit un coup complet : quel pion, depuis où, vers où, de quel type.
// HeuristicScore a deux rôles selon le contexte :
//   - Dans GetValidMoves : score de priorité pour le tri Move Ordering
//   - Dans GetBestMove   : score Minimax final, transmis au thread principal
//                          via BestMoveResult[1].HeuristicScore
// =============================================================================
public struct JobYokaiMove
{
    /// <summary>Données du pion concerné (type, camp, position source).</summary>
    public JobPawnState Pawn;
    /// <summary>Position de départ. Vaut (-1,-1) pour un PARACHUTE depuis la réserve.</summary>
    public Vector2Int SourcePosition;
    /// <summary>Case cible du déplacement ou du parachutage.</summary>
    public Vector2Int Destination;
    /// <summary>MOVE = déplacement sur le plateau, PARACHUTE = depuis la réserve.</summary>
    public EActionType ActionType;
    /// <summary>false si la struct est à sa valeur par défaut (non initialisée).</summary>
    public bool IsValid;
    /// <summary>Score de priorité (Move Ordering) réutilisé comme score Minimax final.</summary>
    public int HeuristicScore;
}

// =============================================================================
// STRUCT : JobGameState
// =============================================================================
// Snapshot complet et autonome du jeu à un instant donné de la simulation.
// Toutes les données sont des types de valeur — copier par affectation
// (JobGameState next = state) crée une copie indépendante sans effets de bord.
//
// Deux représentations redondantes, toujours synchronisées par ApplyMove :
//   - Board     : liste des pions avec type, camp et position (recherche par pion)
//   - Bitboards : masques binaires pour les vérifications d'occupation en O(1)
// =============================================================================
public struct JobGameState
{
    /// <summary>Le joueur dont c'est le tour dans cet état simulé.</summary>
    public ECampType CurrentPlayer;

    /// <summary>
    /// Pions sur le plateau. FixedList512Bytes = mémoire fixe sur la pile, sans GC.
    /// Capacité ≈ 25 pions (largement suffisant pour Yokai no Mori).
    /// </summary>
    public FixedList512Bytes<JobPawnState> Board;

    /// <summary>Types des pions en réserve de P1 (capturés, prêts à être parachutés).</summary>
    public FixedList128Bytes<EPawnType> ReserveP1;

    /// <summary>Types des pions en réserve de P2.</summary>
    public FixedList128Bytes<EPawnType> ReserveP2;

    /// <summary>
    /// Représentation BitBoard synchronisée avec Board.
    /// Préférer IsCellEmpty / IsCellOccupiedByAlly pour les vérifications d'occupation.
    /// </summary>
    public BitBoardBoard Bitboards;

    /// <summary>
    /// Cherche un pion à une position donnée dans Board (recherche linéaire O(n)).
    /// À utiliser uniquement quand on a besoin du type/camp complet du pion.
    /// Pour savoir si une case est occupée, préférer IsCellEmpty (O(1)).
    /// </summary>
    public JobPawnState GetPawnAt(Vector2Int pos, out bool found)
    {
        for (int i = 0; i < Board.Length; i++)
        {
            if (Board[i].Position == pos) { found = true; return Board[i]; }
        }
        found = false;
        return default;
    }

    /// <summary>Retourne true si la case est occupée par un allié du camp donné. O(1) via BitBoard.</summary>
    public bool IsCellOccupiedByAlly(Vector2Int pos, ECampType camp)
    {
        int bitIndex = BitBoard.PositionToIndex(pos);
        ushort allyBoard = camp == ECampType.PLAYER_ONE ? Bitboards.P1All : Bitboards.P2All;
        return (allyBoard & (1 << bitIndex)) != 0;
    }

    /// <summary>Retourne true si aucune pièce n'occupe cette case. O(1) via BitBoard.</summary>
    public bool IsCellEmpty(Vector2Int pos)
    {
        int bitIndex = BitBoard.PositionToIndex(pos);
        return (Bitboards.AllPieces & (1 << bitIndex)) == 0;
    }
}

// =============================================================================
// STRUCT JOB : YKNMMinimaxJob
// =============================================================================
// Cœur du moteur IA. S'exécute sur un Worker Thread via Unity Jobs System.
// [BurstCompile] compile le C# en code machine SIMD natif (x10 à x20 vs C# standard).
//
// Cycle de vie :
//   1. YKNM_AICompetitor remplit les champs publics (état du jeu, paramètres)
//   2. minimaxJob.Schedule() place le Job dans la file Unity (non bloquant)
//   3. Execute() est appelé sur un Worker Thread (hors thread principal)
//   4. Le résultat est écrit dans BestMoveResult[1]
//   5. YKNM_AICompetitor lit BestMoveResult[1] après jobHandle.Complete()
//
// Note sur le timeout : si YKNM_AICompetitor détecte un timeout, il N'appelle
// PAS Complete() immédiatement. Le Job continue en arrière-plan et est complété
// dans StartTurn() au tour suivant, pendant les animations du coup adverse.
// =============================================================================
[BurstCompile]
public struct YKNMMinimaxJob : IJob
{
    #region Champs — Entrées/Sorties natives (partagées avec le thread principal)

    /// <summary>
    /// Buffer de logs de recherche. Rempli par GetBestMove (nœuds racine)
    /// et Minimax (nœuds internes aux 2 premières profondeurs).
    /// Lu par YKNM_AICompetitor.Update() après Complete().
    /// [NativeDisableContainerSafetyRestriction] requis pour l'écriture depuis Burst.
    /// </summary>
    [NativeDisableContainerSafetyRestriction]
    public NativeList<SearchLog> LogBuffer;

    /// <summary>Nombre maximum de logs écrits par itération (évite la saturation mémoire).</summary>
    private const int MaxLogs = 500;

    /// <summary>
    /// Table de transposition : mémoïsation des positions déjà évaluées.
    /// Clé   : hash Zobrist 64-bit unique à chaque configuration de plateau.
    /// Valeur : TTEntry (score + flag Exact/Lower/Upper + profondeur).
    /// [NativeDisableContainerSafetyRestriction] requis pour Burst + Job.
    /// </summary>
    [NativeDisableContainerSafetyRestriction]
    public NativeHashMap<ulong, TTEntry> TranspositionTable;

    /// <summary>
    /// Clés pseudo-aléatoires 64-bit pour le hachage de Zobrist.
    /// Générées une seule fois avec une seed fixe dans YKNM_AICompetitor.
    /// Taille : (12 cases × 5 types × 2 joueurs) + 2 clés joueur courant = 122 entrées.
    /// </summary>
    [ReadOnly] public NativeArray<ulong> ZobristTable;

    /// <summary>
    /// Killer Moves : 2 coups mémorisés par profondeur.
    /// Layout : [profondeur * 2] = killer principal, [profondeur * 2 + 1] = secondaire.
    /// Persistant entre les itérations du même tour (Iterative Deepening).
    /// [NativeDisableContainerSafetyRestriction] requis pour l'écriture depuis Burst.
    /// </summary>
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<JobYokaiMove> KillerMoves;

    /// <summary>Nombre max de profondeurs pour lesquelles des Killer Moves sont stockés.</summary>
    private const int MaxDepthForKillers = 16;

    /// <summary>
    /// Tableau de sortie partagé avec le thread principal.
    /// [1] : Meilleur coup de l'itération en cours, écrit en fin de GetBestMove.
    ///       Lu par YKNM_AICompetitor.Update() après Complete().
    /// </summary>
    public NativeArray<JobYokaiMove> BestMoveResult;

    #endregion

    #region Champs — Paramètres de recherche

    /// <summary>
    /// Score de la dernière profondeur complète (Iterative Deepening).
    /// Centre de la fenêtre d'Aspiration Windows : [PreviousScore-50, PreviousScore+50].
    /// Vaut 0 pour la profondeur 1 (pas de référence disponible).
    /// </summary>
    [ReadOnly] public int PreviousScore;

    /// <summary>
    /// Active les Aspiration Windows si true (profondeur > 1).
    /// Désactivé à la profondeur 1 car il n'existe pas encore de score de référence.
    /// </summary>
    [ReadOnly] public bool UseAspirationWindow;

    // Dimensions du plateau — constantes pour éviter les magic numbers
    private const int BoardWidth = 3;
    private const int BoardHeight = 4;
    private const int NumPieceTypes = 5;
    private const int NumPlayers = 2;

    /// <summary>Camp que cette instance d'IA représente (PLAYER_ONE ou PLAYER_TWO).</summary>
    [ReadOnly] public ECampType AICamp;

    /// <summary>Profondeur maximale d'exploration pour cette itération du Job.</summary>
    [ReadOnly] public int MaxDepth;

    /// <summary>État initial du plateau fourni par le thread principal avant Schedule().</summary>
    [ReadOnly] public NativeList<JobPawnState> InitialBoard;
    [ReadOnly] public NativeList<EPawnType> InitialReserveP1;
    [ReadOnly] public NativeList<EPawnType> InitialReserveP2;

    #endregion

    // =========================================================================
    #region Point d'entrée — Execute
    // =========================================================================

    /// <summary>
    /// Appelé par Unity sur le Worker Thread lors de l'exécution du Job.
    /// Reconstruit le JobGameState depuis les listes natives (NativeList → FixedList),
    /// initialise les BitBoards en même temps pour éviter un second parcours,
    /// puis lance la recherche Minimax via GetBestMove.
    /// </summary>
    public void Execute()
    {
        JobGameState initialState = new JobGameState { CurrentPlayer = AICamp };

        for (int i = 0; i < InitialBoard.Length; i++)
        {
            JobPawnState p = InitialBoard[i];
            initialState.Board.Add(p);
            initialState.Bitboards.SetPiece(p.Type, p.Owner, BitBoard.PositionToIndex(p.Position));
        }

        for (int i = 0; i < InitialReserveP1.Length; i++) initialState.ReserveP1.Add(InitialReserveP1[i]);
        for (int i = 0; i < InitialReserveP2.Length; i++) initialState.ReserveP2.Add(InitialReserveP2[i]);

        // Résultat écrit dans [1] — lu par YKNM_AICompetitor.Update() après Complete()
        BestMoveResult[1] = GetBestMove(initialState);
    }

    #endregion

    // =========================================================================
    #region Recherche racine — GetBestMove
    // =========================================================================

    /// <summary>
    /// Nœud racine de la recherche Minimax. Évalue tous les coups légaux de l'IA
    /// et retourne celui qui obtient le meilleur score.
    ///
    /// ASPIRATION WINDOWS :
    /// Si UseAspirationWindow est true, on commence dans la fenêtre
    /// [PreviousScore - 50, PreviousScore + 50] plutôt que [-∞, +∞].
    /// Si le score d'un coup sort de cette fenêtre (fail-low ou fail-high),
    /// on relance avec la fenêtre complète pour ce coup uniquement.
    /// Gain typique : 10 à 20% de nœuds en moins par profondeur successive.
    ///
    /// Le score final est écrit dans BestMoveResult[1].HeuristicScore pour
    /// alimenter PreviousScore du prochain Job (Aspiration Windows suivant).
    /// </summary>
    private JobYokaiMove GetBestMove(JobGameState state)
    {
        JobYokaiMove bestMove = default;
        bestMove.IsValid = false;
        int bestScore = int.MinValue;
        const int WindowSize = 50;

        int alpha = UseAspirationWindow ? PreviousScore - WindowSize : int.MinValue;
        int beta = UseAspirationWindow ? PreviousScore + WindowSize : int.MaxValue;

        FixedList4096Bytes<JobYokaiMove> moves = GetValidMoves(state, AICamp, MaxDepth);
        if (moves.Length == 0) return bestMove;

        for (int i = 0; i < moves.Length; i++)
        {
            JobYokaiMove move = moves[i];
            JobGameState nextState = ApplyMove(state, move);
            int score = Minimax(nextState, MaxDepth - 1, alpha, beta, false, true);

            // Score hors fenêtre → relance avec fenêtre complète
            if (score <= alpha || score >= beta)
            {
                alpha = int.MinValue;
                beta = int.MaxValue;
                score = Minimax(nextState, MaxDepth - 1, alpha, beta, false, true);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
                bestMove.IsValid = true;
            }

            alpha = Mathf.Max(alpha, bestScore);

            if (LogBuffer.IsCreated && LogBuffer.Length < MaxLogs)
                LogBuffer.Add(new SearchLog
                {
                    Depth = MaxDepth,
                    Score = score,
                    Alpha = alpha,
                    Beta = beta,
                    IsMaximizing = true,
                    BestMove = bestMove,
                    IsRootNode = true
                });
        }

        // Réutilise HeuristicScore pour transmettre le score final au thread principal
        BestMoveResult[1] = new JobYokaiMove
        {
            Pawn = bestMove.Pawn,
            SourcePosition = bestMove.SourcePosition,
            Destination = bestMove.Destination,
            ActionType = bestMove.ActionType,
            IsValid = bestMove.IsValid,
            HeuristicScore = bestScore
        };

        return bestMove;
    }

    #endregion

    // =========================================================================
    #region Algorithme Minimax récursif
    // =========================================================================

    /// <summary>
    /// Cœur de l'algorithme. Explore récursivement l'arbre des coups possibles.
    ///
    /// ÉLAGAGE ALPHA-BETA :
    ///   alpha = meilleur score garanti pour MAX (l'IA).
    ///   beta  = meilleur score garanti pour MIN (l'adversaire).
    ///   Si alpha >= beta, la branche est abandonnée.
    ///
    /// TABLE DE TRANSPOSITION :
    ///   Lookup avant exploration. Si l'entrée est assez profonde et son flag
    ///   est Exact, on retourne immédiatement. Sinon on rétrécit la fenêtre.
    ///   Après exploration, on stocke le résultat avec le flag correct.
    ///
    /// NULL MOVE PRUNING :
    ///   Si passer son tour (null move) ne permet pas à l'adversaire de battre
    ///   beta, la branche est élagée. Réduit agressivement les nœuds explorés.
    ///   Désactivé sur les positions avec peu de pièces et en double-null.
    ///
    /// KILLER MOVES :
    ///   Les coups ayant causé une coupure beta sont mémorisés et priorisés
    ///   (score 85 dans GetValidMoves) aux prochains nœuds de même profondeur.
    /// </summary>
    /// <param name="state">État du jeu à évaluer.</param>
    /// <param name="depth">Profondeur restante (0 = évaluation heuristique directe).</param>
    /// <param name="alpha">Borne inférieure : meilleur score que MAX est sûr d'obtenir.</param>
    /// <param name="beta">Borne supérieure : meilleur score que MIN est sûr d'obtenir.</param>
    /// <param name="isMaximizing">true = tour IA (MAX), false = tour adversaire (MIN).</param>
    /// <param name="allowNullMove">false si un Null Move a déjà été joué au tour précédent.</param>
    private int Minimax(JobGameState state, int depth, int alpha, int beta,
                        bool isMaximizing, bool allowNullMove = true)
    {
        int alphaOrig = alpha; // Sauvegarde pour déterminer le flag TT en fin de recherche

        // --- TABLE DE TRANSPOSITION : lookup ---
        ulong hash = GetZobristHash(state);
        if (TranspositionTable.TryGetValue(hash, out TTEntry entry) && entry.Depth >= depth)
        {
            if (entry.Flag == 0) return entry.Score;                         // Exact
            if (entry.Flag == 1 && entry.Score > alpha) alpha = entry.Score; // LowerBound
            if (entry.Flag == 2 && entry.Score < beta) beta = entry.Score; // UpperBound
            if (alpha >= beta) return entry.Score;
        }

        // --- CONDITIONS D'ARRÊT ---
        if (IsTerminal(state))
        {
            bool aiKingAlive = (AICamp == ECampType.PLAYER_ONE
                ? state.Bitboards.P1_Koropokkuru.Value
                : state.Bitboards.P2_Koropokkuru.Value) != 0;
            bool oppKingAlive = (AICamp == ECampType.PLAYER_ONE
                ? state.Bitboards.P2_Koropokkuru.Value
                : state.Bitboards.P1_Koropokkuru.Value) != 0;

            if (!aiKingAlive) return -1000000;
            if (!oppKingAlive) return 1000000;
            return Evaluate(state);
        }

        if (depth == 0) return Evaluate(state);

        // --- NULL MOVE PRUNING ---
        // Passe le tour et vérifie si l'adversaire ne peut pas battre beta même avec 2 coups
        if (!isMaximizing && allowNullMove && depth >= 3 && state.Board.Length > 2)
        {
            JobGameState nullState = state;
            nullState.CurrentPlayer = Opponent(state.CurrentPlayer);
            int nullScore = Minimax(nullState, depth - 3, alpha, beta, true, false);
            if (nullScore >= beta) return beta;
        }

        FixedList4096Bytes<JobYokaiMove> moves = GetValidMoves(state, state.CurrentPlayer, depth);
        if (moves.Length == 0) return state.CurrentPlayer == AICamp ? -100000 : 100000;

        bool shouldLog = LogBuffer.IsCreated && LogBuffer.Length < MaxLogs && depth >= MaxDepth - 2;
        int best;

        if (isMaximizing) // Tour de l'IA : cherche le score le plus grand
        {
            best = int.MinValue;
            for (int i = 0; i < moves.Length; i++)
            {
                JobGameState next = ApplyMove(state, moves[i]);
                int score = Minimax(next, depth - 1, alpha, beta, false, true);
                best = Mathf.Max(best, score);
                alpha = Mathf.Max(alpha, best);

                if (shouldLog)
                    LogBuffer.Add(new SearchLog
                    {
                        Depth = depth,
                        Score = score,
                        Alpha = alpha,
                        Beta = beta,
                        IsMaximizing = true,
                        IsRootNode = false
                    });

                if (alpha >= beta) { StoreKillerMove(moves[i], depth); break; } // Coupure beta
            }
        }
        else // Tour de l'adversaire : cherche le score le plus petit
        {
            best = int.MaxValue;
            for (int i = 0; i < moves.Length; i++)
            {
                JobGameState next = ApplyMove(state, moves[i]);
                int score = Minimax(next, depth - 1, alpha, beta, true, true);
                best = Mathf.Min(best, score);
                beta = Mathf.Min(beta, best);

                if (shouldLog)
                    LogBuffer.Add(new SearchLog
                    {
                        Depth = depth,
                        Score = score,
                        Alpha = alpha,
                        Beta = beta,
                        IsMaximizing = false,
                        IsRootNode = false
                    });

                if (alpha >= beta) { StoreKillerMove(moves[i], depth); break; } // Coupure alpha
            }
        }

        // --- STOCKAGE DANS LA TABLE DE TRANSPOSITION avec flag correct ---
        TTEntry newEntry;
        newEntry.Score = best;
        newEntry.Depth = depth;
        if (best <= alphaOrig) newEntry.Flag = 2; // UpperBound (fail-low)
        else if (best >= beta) newEntry.Flag = 1; // LowerBound (fail-high)
        else newEntry.Flag = 0; // Exact

        TranspositionTable.TryAdd(hash, newEntry);
        return best;
    }

    #endregion

    // =========================================================================
    #region Génération et tri des coups — GetValidMoves
    // =========================================================================

    /// <summary>
    /// Génère tous les coups légaux du camp donné, puis les trie par score
    /// heuristique décroissant (Move Ordering) pour maximiser les coupures Alpha-Beta.
    ///
    /// HIÉRARCHIE DES SCORES :
    ///   10000 = Capture du Roi (victoire immédiate, priorité absolue)
    ///   100+  = Capture d'une autre pièce (+ valeur de la pièce capturée)
    ///   90    = Promotion Kodama → KodamaSamurai imminente
    ///   85    = Killer Move mémorisé à cette profondeur
    ///   20    = Parachutage depuis la réserve
    ///   10    = Déplacement vers l'avant (avancée tactique)
    ///   0     = Autres déplacements
    ///
    /// BITBOARDS pour les parachutages :
    /// Au lieu de boucler 12 fois sur les cases et appeler IsCellEmpty,
    /// on itère sur le masque EmptyCells via tzcnt (trailing zero count = O(1)),
    /// ne visitant que les cases réellement vides.
    ///
    /// TRI par Selection Sort : déterministe et compatible Burst (pas de lambda).
    /// </summary>
    /// <param name="currentDepth">Profondeur courante — pour consulter les Killer Moves.</param>
    private FixedList4096Bytes<JobYokaiMove> GetValidMoves(JobGameState state, ECampType camp, int currentDepth)
    {
        FixedList4096Bytes<JobYokaiMove> moves = new FixedList4096Bytes<JobYokaiMove>();

        // --- 1. DÉPLACEMENTS (MOVE) ---
        for (int i = 0; i < state.Board.Length; i++)
        {
            JobPawnState pawn = state.Board[i];
            if (pawn.Owner != camp) continue;

            FixedList128Bytes<Vector2Int> dirs = GetDirectionsLocal(pawn.Type, camp);

            for (int d = 0; d < dirs.Length; d++)
            {
                Vector2Int dest = pawn.Position + dirs[d];
                if (!IsInBounds(dest)) continue;
                if (state.IsCellOccupiedByAlly(dest, camp)) continue; // O(1) via BitBoard

                bool hasOccupant = !state.IsCellEmpty(dest);
                JobPawnState occupant = hasOccupant ? state.GetPawnAt(dest, out _) : default;

                JobYokaiMove m = new JobYokaiMove
                {
                    Pawn = pawn,
                    SourcePosition = pawn.Position,
                    Destination = dest,
                    ActionType = EActionType.MOVE,
                    IsValid = true,
                    HeuristicScore = 0
                };

                if (hasOccupant)
                    m.HeuristicScore = occupant.Type == EPawnType.Koropokkuru
                        ? 10000 : 100 + GetPieceValue(occupant.Type);
                else if (pawn.Type == EPawnType.Kodama && IsEnemyTerritory(camp, dest))
                    m.HeuristicScore = 90;
                else if (IsKillerMove(m, currentDepth))
                    m.HeuristicScore = 85;
                else
                {
                    int forwardDir = camp == ECampType.PLAYER_ONE ? 1 : -1;
                    if ((dest.y - pawn.Position.y) * forwardDir > 0) m.HeuristicScore = 10;
                }

                moves.Add(m);
            }
        }

        // --- 2. PARACHUTAGES (PARACHUTE) ---
        bool isP1 = camp == ECampType.PLAYER_ONE;
        int reserveCount = isP1 ? state.ReserveP1.Length : state.ReserveP2.Length;

        for (int r = 0; r < reserveCount; r++)
        {
            EPawnType pawnType = isP1 ? state.ReserveP1[r] : state.ReserveP2[r];
            JobPawnState virtual1 = new JobPawnState { Type = pawnType, Owner = camp };

            // tzcnt = trailing zero count : index du bit le plus bas = 1ère case vide
            ushort emptyCells = state.Bitboards.EmptyCells;
            while (emptyCells != 0)
            {
                int bitIndex = math.tzcnt((int)emptyCells);
                emptyCells &= (ushort)(emptyCells - 1); // Retire ce bit

                moves.Add(new JobYokaiMove
                {
                    Pawn = virtual1,
                    SourcePosition = new Vector2Int(-1, -1),
                    Destination = BitBoard.IndexToPosition(bitIndex),
                    ActionType = EActionType.PARACHUTE,
                    IsValid = true,
                    HeuristicScore = 20
                });
            }
        }

        // --- 3. TRI DÉCROISSANT par Selection Sort ---
        for (int i = 0; i < moves.Length - 1; i++)
        {
            int maxIndex = i;
            for (int j = i + 1; j < moves.Length; j++)
                if (moves[j].HeuristicScore > moves[maxIndex].HeuristicScore) maxIndex = j;

            if (maxIndex != i)
            {
                JobYokaiMove temp = moves[i];
                moves[i] = moves[maxIndex];
                moves[maxIndex] = temp;
            }
        }

        return moves;
    }

    #endregion

    // =========================================================================
    #region Application d'un coup — ApplyMove
    // =========================================================================

    /// <summary>
    /// Retourne un nouvel état résultant de l'application du coup.
    /// L'état original n'est JAMAIS modifié (copie par valeur : JobGameState next = state).
    /// Gère : déplacements, captures, promotion automatique du Kodama, parachutages.
    /// Maintient la synchronisation Board ↔ Bitboards après chaque modification.
    /// </summary>
    private JobGameState ApplyMove(JobGameState state, JobYokaiMove move)
    {
        JobGameState next = state; // Copie complète par valeur

        if (move.ActionType == EActionType.PARACHUTE)
        {
            // Retire le pion de la réserve appropriée
            int index = -1;
            if (move.Pawn.Owner == ECampType.PLAYER_ONE)
            {
                for (int i = 0; i < next.ReserveP1.Length; i++)
                    if (next.ReserveP1[i] == move.Pawn.Type) { index = i; break; }
                if (index != -1) next.ReserveP1.RemoveAt(index);
            }
            else
            {
                for (int i = 0; i < next.ReserveP2.Length; i++)
                    if (next.ReserveP2[i] == move.Pawn.Type) { index = i; break; }
                if (index != -1) next.ReserveP2.RemoveAt(index);
            }

            next.Board.Add(new JobPawnState
            {
                Position = move.Destination,
                Type = move.Pawn.Type,
                Owner = move.Pawn.Owner
            });
            next.Bitboards.SetPiece(move.Pawn.Type, move.Pawn.Owner,
                BitBoard.PositionToIndex(move.Destination));
        }
        else // MOVE
        {
            int srcBit = BitBoard.PositionToIndex(move.SourcePosition);
            int destBit = BitBoard.PositionToIndex(move.Destination);

            // Capture éventuelle
            int enemyIndex = -1;
            for (int i = 0; i < next.Board.Length; i++)
                if (next.Board[i].Position == move.Destination) { enemyIndex = i; break; }

            if (enemyIndex != -1)
            {
                JobPawnState captured = next.Board[enemyIndex];
                // KodamaSamurai capturé redevient Kodama dans la réserve adverse
                EPawnType typeToAdd = captured.Type == EPawnType.KodamaSamurai
                    ? EPawnType.Kodama : captured.Type;

                next.Bitboards.ClearPiece(captured.Type, captured.Owner, destBit);
                if (move.Pawn.Owner == ECampType.PLAYER_ONE) next.ReserveP1.Add(typeToAdd);
                else next.ReserveP2.Add(typeToAdd);
                next.Board.RemoveAt(enemyIndex);
            }

            // Déplacement du pion allié
            for (int i = 0; i < next.Board.Length; i++)
            {
                if (next.Board[i].Position == move.SourcePosition && next.Board[i].Owner == move.Pawn.Owner)
                {
                    JobPawnState updated = next.Board[i];
                    next.Bitboards.ClearPiece(updated.Type, updated.Owner, srcBit);
                    updated.Position = move.Destination;

                    // Promotion automatique : Kodama sur la ligne adverse → KodamaSamurai
                    if (updated.Type == EPawnType.Kodama && IsEnemyTerritory(updated.Owner, move.Destination))
                        updated.Type = EPawnType.KodamaSamurai;

                    next.Bitboards.SetPiece(updated.Type, updated.Owner, destBit);
                    next.Board[i] = updated;
                    break;
                }
            }
        }

        next.CurrentPlayer = Opponent(state.CurrentPlayer);
        return next;
    }

    #endregion

    // =========================================================================
    #region Détection de fin de partie — IsTerminal
    // =========================================================================

    /// <summary>
    /// Détecte une position terminale (fin de partie). Deux conditions :
    ///   1. Le Koropokkuru d'un joueur a été capturé (BitBoard à 0)
    ///   2. Le Koropokkuru d'un joueur est sur la ligne adverse :
    ///      P1 gagne si son roi est sur y=3 (bits 9-11, masque 0b111000000000)
    ///      P2 gagne si son roi est sur y=0 (bits 0-2,  masque 0b000000000111)
    /// Toutes les vérifications sont O(1) via les BitBoards.
    /// </summary>
    private bool IsTerminal(JobGameState state)
    {
        if (state.Bitboards.P1_Koropokkuru.Value == 0 ||
            state.Bitboards.P2_Koropokkuru.Value == 0) return true;

        if ((state.Bitboards.P1_Koropokkuru.Value & 0b111000000000) != 0) return true; // P1 sur y=3
        if ((state.Bitboards.P2_Koropokkuru.Value & 0b000000000111) != 0) return true; // P2 sur y=0
        return false;
    }

    #endregion

    // =========================================================================
    #region Évaluation heuristique — Evaluate
    // =========================================================================

    /// <summary>
    /// Attribue un score à un état non terminal (du point de vue de l'IA).
    /// Score positif = avantage IA, négatif = avantage adversaire.
    ///
    /// COMPOSANTES :
    ///   - Survie des rois        : ±100000 (via BitBoard O(1))
    ///   - Position sur le trône  : ±90000  (via masque binaire O(1))
    ///   - Score matériel         : somme des valeurs des pièces sur le plateau
    ///   - Bonus de proximité roi : (3 - distance au trône) * 200 pour le Koropokkuru
    ///   - Réserves               : valeur complète (pièces parachutables immédiatement)
    ///
    /// VALEURS DES PIÈCES :
    ///   Koropokkuru=1000, KodamaSamurai=40, Kitsune=30, Tanuki=30, Kodama=20
    /// </summary>
    private int Evaluate(JobGameState state)
    {
        bool aiKingAlive = (AICamp == ECampType.PLAYER_ONE
            ? state.Bitboards.P1_Koropokkuru.Value
            : state.Bitboards.P2_Koropokkuru.Value) != 0;
        bool oppKingAlive = (AICamp == ECampType.PLAYER_ONE
            ? state.Bitboards.P2_Koropokkuru.Value
            : state.Bitboards.P1_Koropokkuru.Value) != 0;

        if (!aiKingAlive) return -100000;
        if (!oppKingAlive) return 100000;

        bool aiKingOnThrone = AICamp == ECampType.PLAYER_ONE
            ? (state.Bitboards.P1_Koropokkuru.Value & 0b111000000000) != 0
            : (state.Bitboards.P2_Koropokkuru.Value & 0b000000000111) != 0;
        bool oppKingOnThrone = AICamp == ECampType.PLAYER_ONE
            ? (state.Bitboards.P2_Koropokkuru.Value & 0b000000000111) != 0
            : (state.Bitboards.P1_Koropokkuru.Value & 0b111000000000) != 0;

        if (aiKingOnThrone) return 90000;
        if (oppKingOnThrone) return -90000;

        int score = 0;

        // Score matériel + bonus de proximité du trône pour le Koropokkuru
        for (int i = 0; i < state.Board.Length; i++)
        {
            JobPawnState pawn = state.Board[i];
            int value = GetPieceValue(pawn.Type);

            if (pawn.Type == EPawnType.Koropokkuru)
            {
                // Bonus de 0 à 600 : dist=3 → +0, dist=2 → +200, dist=1 → +400, dist=0 → +600
                int distToThrone = pawn.Owner == ECampType.PLAYER_ONE
                    ? 3 - pawn.Position.y : pawn.Position.y;
                value += (3 - distToThrone) * 200;
            }

            score += pawn.Owner == AICamp ? value : -value;
        }

        // Score des réserves : pièces parachutables immédiatement valent leur pleine valeur
        int myReserveCount = AICamp == ECampType.PLAYER_ONE ? state.ReserveP1.Length : state.ReserveP2.Length;
        int oppReserveCount = AICamp == ECampType.PLAYER_ONE ? state.ReserveP2.Length : state.ReserveP1.Length;

        for (int i = 0; i < myReserveCount; i++)
        {
            EPawnType type = AICamp == ECampType.PLAYER_ONE ? state.ReserveP1[i] : state.ReserveP2[i];
            score += GetPieceValue(type);
        }
        for (int i = 0; i < oppReserveCount; i++)
        {
            EPawnType type = AICamp == ECampType.PLAYER_ONE ? state.ReserveP2[i] : state.ReserveP1[i];
            score -= GetPieceValue(type);
        }

        return score;
    }

    #endregion

    // =========================================================================
    #region Hachage de Zobrist — GetZobristHash
    // =========================================================================

    /// <summary>
    /// Calcule une empreinte 64-bit unique représentant l'état complet du jeu.
    /// Utilisée comme clé de la table de transposition.
    ///
    /// PRINCIPE :
    /// Chaque (case, type, joueur) est associé à un nombre 64-bit aléatoire.
    /// Le hash est le XOR de tous ces nombres pour les pièces présentes.
    /// Le XOR garantit des collisions rarissimes, et permet des mises à jour
    /// incrémentales en O(1) si nécessaire à l'avenir.
    ///
    /// INDEXATION :
    /// index = (case * 5 * 2) + (type * 2) + joueur (0 pour P1, 1 pour P2)
    /// Les deux derniers slots encodent le joueur courant, pour éviter qu'une
    /// même position avec un tour différent partage le même hash.
    /// </summary>
    private ulong GetZobristHash(JobGameState state)
    {
        ulong hash = 0;

        for (int i = 0; i < state.Board.Length; i++)
        {
            var pawn = state.Board[i];
            int posIndex = pawn.Position.x + (pawn.Position.y * BoardWidth);
            int pieceIndex = (int)pawn.Type;
            int playerIdx = pawn.Owner == ECampType.PLAYER_ONE ? 0 : 1;
            int zobristIndex = (posIndex * NumPieceTypes * NumPlayers) + (pieceIndex * NumPlayers) + playerIdx;
            hash ^= ZobristTable[zobristIndex];
        }

        int playerOffset = state.CurrentPlayer == ECampType.PLAYER_ONE ? 0 : 1;
        int currentPlayerIdx = (BoardWidth * BoardHeight * NumPieceTypes * NumPlayers) + playerOffset;
        hash ^= ZobristTable[currentPlayerIdx];

        return hash;
    }

    #endregion

    // =========================================================================
    #region Killer Moves — StoreKillerMove / IsKillerMove
    // =========================================================================

    /// <summary>
    /// Mémorise un coup ayant causé une coupure Beta à la profondeur donnée.
    /// Stocke 2 killers par profondeur en FIFO :
    /// le nouveau killer devient slot0, l'ancien slot0 devient slot1.
    /// Seuls les MOVE sont stockés (les PARACHUTE sont moins réutilisables).
    /// </summary>
    private void StoreKillerMove(JobYokaiMove move, int depth)
    {
        if (move.ActionType != EActionType.MOVE) return;
        if (depth >= MaxDepthForKillers) return;

        int slot0 = depth * 2;
        int slot1 = depth * 2 + 1;

        // Ne stocke pas si ce coup est déjà le killer principal
        if (KillerMoves[slot0].IsValid
            && KillerMoves[slot0].SourcePosition == move.SourcePosition
            && KillerMoves[slot0].Destination == move.Destination)
            return;

        KillerMoves[slot1] = KillerMoves[slot0];
        KillerMoves[slot0] = move;
    }

    /// <summary>
    /// Vérifie si un coup correspond à l'un des 2 Killer Moves de la profondeur donnée.
    /// Utilisé dans GetValidMoves pour attribuer un score de priorité 85 à ces coups.
    /// </summary>
    private bool IsKillerMove(JobYokaiMove move, int depth)
    {
        if (move.ActionType != EActionType.MOVE) return false;
        if (depth >= MaxDepthForKillers) return false;

        int slot0 = depth * 2;
        int slot1 = depth * 2 + 1;

        if (KillerMoves[slot0].IsValid
            && KillerMoves[slot0].SourcePosition == move.SourcePosition
            && KillerMoves[slot0].Destination == move.Destination) return true;

        if (KillerMoves[slot1].IsValid
            && KillerMoves[slot1].SourcePosition == move.SourcePosition
            && KillerMoves[slot1].Destination == move.Destination) return true;

        return false;
    }

    #endregion

    // =========================================================================
    #region Utilitaires — GetDirectionsLocal / IsEnemyTerritory / GetPieceValue / IsInBounds / Opponent
    // =========================================================================

    /// <summary>
    /// Retourne les directions de déplacement légales pour chaque type de pion.
    /// "forward" = +1 (vers y croissant) pour P1, -1 pour P2.
    /// FixedList128Bytes = allouée sur la pile, sans GC, compatible Burst.
    /// </summary>
    private FixedList128Bytes<Vector2Int> GetDirectionsLocal(EPawnType type, ECampType camp)
    {
        FixedList128Bytes<Vector2Int> dirs = new FixedList128Bytes<Vector2Int>();
        int forward = camp == ECampType.PLAYER_ONE ? 1 : -1;

        switch (type)
        {
            case EPawnType.Koropokkuru: // Roi : 8 directions
                dirs.Add(new Vector2Int(0, 1)); dirs.Add(new Vector2Int(0, -1));
                dirs.Add(new Vector2Int(1, 0)); dirs.Add(new Vector2Int(-1, 0));
                dirs.Add(new Vector2Int(1, 1)); dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1)); dirs.Add(new Vector2Int(-1, -1));
                break;
            case EPawnType.Kodama: // Pion : 1 case en avant
                dirs.Add(new Vector2Int(0, forward));
                break;
            case EPawnType.Kitsune: // Fou : 4 diagonales
                dirs.Add(new Vector2Int(1, 1)); dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1)); dirs.Add(new Vector2Int(-1, -1));
                break;
            case EPawnType.Tanuki: // Tour : 4 cardinales
                dirs.Add(new Vector2Int(0, 1)); dirs.Add(new Vector2Int(0, -1));
                dirs.Add(new Vector2Int(1, 0)); dirs.Add(new Vector2Int(-1, 0));
                break;
            case EPawnType.KodamaSamurai: // Général d'or : 6 directions (pas diagonales arrière)
                dirs.Add(new Vector2Int(0, forward));
                dirs.Add(new Vector2Int(1, forward)); dirs.Add(new Vector2Int(-1, forward));
                dirs.Add(new Vector2Int(1, 0)); dirs.Add(new Vector2Int(-1, 0));
                dirs.Add(new Vector2Int(0, -forward));
                break;
        }
        return dirs;
    }

    /// <summary>Retourne true si la position est sur la ligne adverse (condition de victoire par trône).</summary>
    private bool IsEnemyTerritory(ECampType camp, Vector2Int position)
        => (camp == ECampType.PLAYER_ONE && position.y == BoardHeight - 1)
        || (camp == ECampType.PLAYER_TWO && position.y == 0);

    /// <summary>
    /// Valeur matérielle de chaque type de pièce.
    /// Koropokkuru = 1000 pour dominer toutes les autres valeurs combinées.
    /// </summary>
    private int GetPieceValue(EPawnType type) => type switch
    {
        EPawnType.Koropokkuru => 1000,
        EPawnType.KodamaSamurai => 40,
        EPawnType.Kitsune => 30,
        EPawnType.Tanuki => 30,
        EPawnType.Kodama => 20,
        _ => 0
    };

    /// <summary>Retourne true si la position est dans les limites du plateau 3x4.</summary>
    private bool IsInBounds(Vector2Int pos)
        => pos.x >= 0 && pos.x < BoardWidth && pos.y >= 0 && pos.y < BoardHeight;

    /// <summary>Retourne le camp opposé.</summary>
    private ECampType Opponent(ECampType camp)
        => camp == ECampType.PLAYER_ONE ? ECampType.PLAYER_TWO : ECampType.PLAYER_ONE;

    #endregion
}