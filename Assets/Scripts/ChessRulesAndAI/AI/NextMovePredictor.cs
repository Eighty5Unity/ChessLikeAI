using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextMovePredictor : MonoBehaviour
{
    AIBoardState m_lastBoard = null;
    AIMovesGenerator.PossibleMove m_lastMove;
    public int MoveNumber = 0;

    public int DebugStopAtMove = 4;

    public UnityEngine.UI.Text ProgressText = null;

    bool m_calculatingAI = false;

    public int DepthNormal = 7;
    public int QuiscenceDepth = 11;
    public bool UseThreading = true;
    public AIMovesGenerator.PossibleMove MoveToPerform { get { return m_lastMove; } }
    public bool CalculatingMove { get { return m_calculatingAI; } }
    public TreeNode treeNode;
    public int bestMoveIdx;
    public bool isThreadFinish;

    public void Predict(AIBoardState boardState)
    {
        if (m_calculatingAI)
            return;

        MoveNumber = 0;
        StartCoroutine(PredictInternal(boardState, UseThreading, treeNode));
    }
    public void PredictNext()
    {
        if (m_calculatingAI)
            return;

        ++MoveNumber;
        PredictInternal(m_lastBoard, false, treeNode);
    }

    public IEnumerator PredictInternal(AIBoardState currentBoard, bool useThreading, TreeNode treeNode)
    {
        m_calculatingAI = true;

        AIMovesGenerator.s_numGeneratedAttacks = 0;
        AIMovesGenerator.s_numGeneratedMoves = 0;
        AIMovesTree.s_lowestDepth = int.MaxValue;

        if (ProgressText !=null)
            ProgressText.text = "Started";
        
        float startTime = Time.realtimeSinceStartup;

        if (useThreading)
            yield return PredictInternalSingleThread(currentBoard, treeNode);
        //yield return PredictInternalWithThreading(currentBoard);
        else
            yield return PredictInternalNoThreading(currentBoard, treeNode);
        

        if (ProgressText != null)
            ProgressText.text = "Finished";

        //Debug.Log("Time spent " + (Time.realtimeSinceStartup - startTime));
        //Debug.Log("Total moves " + (AIMovesGenerator.s_numGeneratedMoves + AIMovesGenerator.s_numGeneratedAttacks));
        //Debug.Log("Total attacks " + AIMovesGenerator.s_numGeneratedAttacks);
        //Debug.Log("Total moves " + AIMovesGenerator.s_numGeneratedMoves);
        //Debug.Log("Lowest depth " + AIMovesTree.s_lowestDepth);

        //Debug.Log("Total TreeNode " + AIMovesTree.countOfTree);

        m_calculatingAI = false;
    }

    IEnumerator PredictInternalWithThreading(AIBoardState currentBoard)
    {
        var tree = AIMovesTreeThreaded.CalculateScoreWithThreadingChildren(true, currentBoard, int.MinValue, int.MaxValue, DepthNormal + 1, QuiscenceDepth + 1, false);

        while (tree.Thread.IsAlive)
            yield return null;
        
        AIMovesGenerator generator = new AIMovesGenerator();
        generator.InitializeForBoard(currentBoard);

        generator.GenerateAllMoves(0);
        generator.SortAttackMovesFirst();

        if (tree.Tree.BestMoveIdx >= 0)
        {
            m_lastMove = generator.PossibleMoves[tree.Tree.BestMoveIdx];
            generator.ApplyMoveToBoard(m_lastMove);
            m_lastBoard = currentBoard.Clone();
        }
        else
            Debug.Log("No possible moves");
    }


    IEnumerator PredictInternalSingleThread(AIBoardState currentBoard, TreeNode treeNode)
    {
        var tree = AIMovesTreeThreaded.CalculateScoreOnSingleThread(true, currentBoard, int.MinValue, int.MaxValue, DepthNormal + 1, QuiscenceDepth + 1, Constants.False, treeNode);

        isThreadFinish = false;//Use this flag in Next Move button

        while (tree.Thread.IsAlive)
            yield return null;


        Debug.Log("Total " + (AIMovesGenerator.s_numGeneratedMoves + AIMovesGenerator.s_numGeneratedAttacks));
        Debug.Log("attacks " + AIMovesGenerator.s_numGeneratedAttacks);
        Debug.Log("moves " + AIMovesGenerator.s_numGeneratedMoves);
        Debug.Log("TreeNodes " + AIMovesTree.numTreeNodes);
        Debug.Log($"BEST IDX {tree.Tree.BestMoveIdx}");
        bestMoveIdx = tree.Tree.BestMoveIdx;

        isThreadFinish = true;

        AIMovesGenerator generator = new AIMovesGenerator();
        generator.InitializeForBoard(currentBoard);

        generator.GenerateAllMoves(0);
        //generator.SortAttackMovesFirst(); - this generator not equal TreeNode

        PrintMoves(generator.PossibleMoves, tree.Tree.BestMoveIdx);

        //if (tree.Tree.BestMoveIdx >= 0)
        //{
        //    m_lastMove = generator.PossibleMoves[tree.Tree.BestMoveIdx];
        //    generator.ApplyMoveToBoard(m_lastMove);
        //    m_lastBoard = currentBoard.Clone();
        //}
        //else
        //    Debug.Log("No possible moves");
    }

    IEnumerator PredictInternalNoThreading(AIBoardState currentBoard, TreeNode treeNode)
    {
        List<AIMovesGenerator.PossibleMove> moves = new List<AIMovesGenerator.PossibleMove>(40);

        AIMovesGenerator generator = new AIMovesGenerator();
        generator.InitializeForBoard(currentBoard);

        generator.GenerateAllMoves(0);
        generator.SortAttackMovesFirst();


        int alpha = int.MinValue;
        int beta = int.MaxValue;

        int bestValue = int.MinValue;
        int bestMoveIdx = -1;

        for (int i = 0; i < generator.NumMoves; ++i)
        {
            generator.ApplyMoveToBoard(i);

            string progressText = "Move " + i + " / " + generator.NumMoves;

            if (ProgressText != null)
                ProgressText.text = progressText;
            
            //Debug.Log(progressText);

            AIMovesTree tree = new AIMovesTree();
            tree.Create(currentBoard);

            /*
            if ((AIBoardState.GetX(generator.PossibleMoves[i].SourceFieldBefore.FieldIdx) == 5) && (AIBoardState.GetZ(generator.PossibleMoves[i].SourceFieldBefore.FieldIdx) == 6)
                && (AIBoardState.GetX(generator.PossibleMoves[i].DestFieldBefore.FieldIdx) == 5) && (AIBoardState.GetZ(generator.PossibleMoves[i].DestFieldBefore.FieldIdx) == 5))
            {
                Debug.Log("Aaaa");
            }
            */

            int moveScore = tree.CalculateScoreMinimizer(0, false, alpha, beta, DepthNormal, QuiscenceDepth, generator.PossibleMoves[i].IsAttack, treeNode);

            if (moveScore > bestValue)
            {
                bestValue = moveScore;

                if (bestValue > alpha)
                {
                    alpha = bestValue;

                    // Super important to get the first move with the highest score, the rest moves with this score thanks to pruning may be WORST (or much worse)
                    // since alpha beta returns a SCORE, not a move so it prunes all moves that have not potential
                    // of generating a higher score so it is "this score or lower so I am not calculating anymore" for all moves that are after the first move giving the best score
                    bestMoveIdx = i;
                }
            }

            AIMovesGenerator.PossibleMove move = generator.PossibleMoves[i];
            move.ScoreAfterMove = moveScore;
            moves.Add(move);

            generator.RevertMoveFromBoard(i);

            yield return null;
        }

        PrintMoves(moves, bestMoveIdx);

        if (bestMoveIdx >= 0)
        {
            m_lastMove = generator.PossibleMoves[bestMoveIdx];
            generator.ApplyMoveToBoard(m_lastMove);
            m_lastBoard = currentBoard.Clone();
        }
        else
            Debug.Log("No possible moves");
    }

    void PrintMoves(List<AIMovesGenerator.PossibleMove> moves, int bestMoveIdx)
    {
        string message = "Num moves " + moves.Count;

        for (int i = 0; i < moves.Count; ++i)
        {
            message += "\n[" + i + "] " + AIBoardState.GetX(moves[i].SourceFieldBefore.FieldIdx) + " : " + AIBoardState.GetZ(moves[i].SourceFieldBefore.FieldIdx) +
                            " -> " + AIBoardState.GetX(moves[i].DestFieldAfter.FieldIdx) + " : " + AIBoardState.GetZ(moves[i].DestFieldAfter.FieldIdx);
            string scoreMessage;

            if (i != bestMoveIdx)
            {
                scoreMessage = " ( " + moves[i].ScoreAfterMove + " or worst ) ";
            }
            else
            {
                scoreMessage = " ( " + moves[i].ScoreAfterMove + " BEST ) ";
                //predictFieldIdxFrom = moves[i].SourceFieldBefore.FieldIdx;
                //predictFieldIdxTo = moves[i].DestFieldAfter.FieldIdx;
            }

            message += scoreMessage;
        }

        Debug.Log(message);
    }

    AIBoardState GetInitialBoardState()
    {
        AIBoardState initialBoar = new AIBoardState();
        initialBoar.Create();
        initialBoar.MovingSide = Constants.ConflictSideWhite;
        initialBoar.MovingSideScoreMul = 1;
        initialBoar.BoardScore = 0;// This is a reference value only? So the board score will be above or below zero


        AddQueen(initialBoar, 0, 0, Constants.ConflictSideWhite, MovementPattern.Queen);
        AddQueen(initialBoar, 0, 4, Constants.ConflictSideBlack, MovementPattern.Queen);

        AddQueen(initialBoar, 1, 0, Constants.ConflictSideWhite, MovementPattern.Rook);
        AddQueen(initialBoar, 1, 4, Constants.ConflictSideBlack, MovementPattern.Rook);

        AddQueen(initialBoar, 2, 0, Constants.ConflictSideWhite, MovementPattern.Rook);
        AddQueen(initialBoar, 2, 4, Constants.ConflictSideBlack, MovementPattern.Rook);

        AddQueen(initialBoar, 3, 0, Constants.ConflictSideWhite, MovementPattern.Bishop);
        AddQueen(initialBoar, 3, 4, Constants.ConflictSideBlack, MovementPattern.Bishop);

        AddQueen(initialBoar, 4, 0, Constants.ConflictSideWhite, MovementPattern.Bishop);
        AddQueen(initialBoar, 4, 4, Constants.ConflictSideBlack, MovementPattern.Bishop);

        AddQueen(initialBoar, 5, 0, Constants.ConflictSideWhite, MovementPattern.Knight);
        AddQueen(initialBoar, 5, 4, Constants.ConflictSideBlack, MovementPattern.Knight);

        AddQueen(initialBoar, 6, 0, Constants.ConflictSideWhite, MovementPattern.Knight);
        AddQueen(initialBoar, 6, 4, Constants.ConflictSideBlack, MovementPattern.Knight);

        AddQueen(initialBoar, 7, 0, Constants.ConflictSideWhite, MovementPattern.King);
        AddQueen(initialBoar, 7, 4, Constants.ConflictSideBlack, MovementPattern.King);
 

        return initialBoar;
    }

    public void AddQueen(AIBoardState currentBoard, int x, int z, byte side, MovementPattern pattern)
    {
        AddPieceToBoard(currentBoard, x, z, side, pattern, 9, 0, 100, 9);
    }

    public void AddPieceToBoard(AIBoardState currentBoard, int x, int z, byte side, MovementPattern movementPattern, int attack, float dodge, int aliveScore, int HP)
    {
        int fieldIdx = AIBoardState.GetFieldIdx(x, z);

        AIBoardState.FieldData field = new AIBoardState.FieldData();
        field.FieldIdx = (byte)fieldIdx;
        field.HasPiece = 1;
        field.Piece.MovementPattern = (byte)movementPattern;
        field.Piece.Attack = (byte)attack;
        field.Piece.FirstBlowDamage = 2;
        field.Piece.OneMinusDodgeChance = 1.0f - dodge;
        field.Piece.AliveScore = (byte)aliveScore;
        field.Piece.HP = (byte)HP;
        field.Piece.Side = (byte)side;

        currentBoard.Board[fieldIdx] = field;
    }
}
