using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using System;
using Unity.Burst;
using System.Runtime.CompilerServices;
using System.Threading;

[BurstCompile(CompileSynchronously = true)]
public class AIMovesTreeWithScore
{
    public int LastScoreVal = 0;
    public bool AbortComputation = false;
    public int BestMoveIdx = 0;
}

[BurstCompile(CompileSynchronously = true)]
public class AIMovesTree : AIMovesTreeWithScore
{
    public static int numTreeNodes;

    public static int s_lowestDepth = 0;

    const int c_minDepthForSortingMoves = -1;

    AIBoardState m_currentBoardState;

    SimplePoolClassFast<AIMovesGenerator> m_moveGeneratorsPool = new SimplePoolClassFast<AIMovesGenerator>();

    public void Create(AIBoardState initialBoardState)
    {
        m_currentBoardState = initialBoardState.Clone();
    }

    public void CalculateScoreMaximizerStoreVal(int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, byte wasLastMoveAnAttack, TreeNode treeNode)
    {
        LastScoreVal = CalculateScoreMaximizer(0, true, alpha, beta, depthRemaining, quiscenceDepthRemaining, wasLastMoveAnAttack, treeNode);
    }

    public void CalculateScoreMinimizerStoreVal(int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, byte wasLastMoveAnAttack, TreeNode treeNode)
    {
        LastScoreVal = CalculateScoreMinimizer(0, true, alpha, beta, depthRemaining, quiscenceDepthRemaining, wasLastMoveAnAttack, treeNode);
    }

    public int CalculateScoreMaximizer(byte currentDepth, bool storeBestMoveIdx, int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, byte wasLastMoveAnAttack, TreeNode treeNode)
    {
        if (depthRemaining < s_lowestDepth)
            s_lowestDepth = depthRemaining;

        if ((depthRemaining == 0) && (wasLastMoveAnAttack == Constants.False))// No quiscence
            return m_currentBoardState.BoardScore;

        if (quiscenceDepthRemaining == 0)
            return m_currentBoardState.BoardScore;

        LinkedListNode<AIMovesGenerator> generatorNode = m_moveGeneratorsPool.GetNewObject();
        AIMovesGenerator generator = generatorNode.Value;
        generator.InitializeForBoard(m_currentBoardState);

        // For quiscence always assume that we do not have to attack, but we can so start with a score of a move
        int bestValue = depthRemaining <= 0 ? m_currentBoardState.BoardScore : int.MinValue;

        bool hadMoves = false;
        int bestMoveIdx = -1;

        int idxChildOfTreeNode = 0;

        while (generator.GenerateNewMoves(depthRemaining <= 0, currentDepth))
        {
            //Don't use sort because the order is not equal to the order after Predict
            //if (depthRemaining > c_minDepthForSortingMoves)
            //    generator.SortAttackMovesFirst();

            CreateTreeNode(treeNode, generator, currentDepth);

            hadMoves = true;

            for (int i = 0; i < generator.NumMoves; ++i)
            {
                generator.ApplyMoveToBoard(i);

                var nextTreeNode = treeNode.children[idxChildOfTreeNode++];

                int moveScore = CalculateScoreMinimizer((byte)(currentDepth + 1), false, alpha, beta, depthRemaining - 1, quiscenceDepthRemaining - 1, generator.PossibleMoves[i].IsAttack, nextTreeNode);

                if (AbortComputation)// When threaded call this is set if alpha beta pruned parent node anyway
                    return bestValue;// Does not atter what we return here

                if (moveScore > bestValue)
                {
                    bestValue = moveScore;
                    bestMoveIdx = idxChildOfTreeNode;

                    if (bestValue > alpha)
                    {
                        alpha = bestValue;

                        if (beta <= alpha)
                        {
                            generator.RevertMoveFromBoard(i);
                            goto Finish;
                        }
                    }
                }

                generator.RevertMoveFromBoard(i);
            }
        }
        treeNode.max = beta;
        treeNode.min = alpha;
        treeNode.boardScore = m_currentBoardState.BoardScore;

    Finish:
        treeNode.max = beta;
        treeNode.min = alpha;

        m_moveGeneratorsPool.ReturnObject(generatorNode);

        if (storeBestMoveIdx)
            BestMoveIdx = bestMoveIdx;

        if (!hadMoves)// Quiscence or some debug situations
            return m_currentBoardState.BoardScore;
        else
            return bestValue;
    }

    public int CalculateScoreMinimizer(byte currentDepth, bool storeBestMoveIdx, int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, byte wasLastMoveAnAttack, TreeNode treeNode)
    {

        if (depthRemaining < s_lowestDepth)
            s_lowestDepth = depthRemaining;

        if ((depthRemaining == 0) && (wasLastMoveAnAttack == Constants.False))// No quiscence
            return m_currentBoardState.BoardScore;

        if (quiscenceDepthRemaining == 0)
            return m_currentBoardState.BoardScore;

        LinkedListNode<AIMovesGenerator> generatorNode = m_moveGeneratorsPool.GetNewObject();
        AIMovesGenerator generator = generatorNode.Value;
        generator.InitializeForBoard(m_currentBoardState);

        // For quiscence always assume that we do not have to attack, but we can so start with a score of a move
        int bestValue = depthRemaining <= 0 ? m_currentBoardState.BoardScore : int.MaxValue;

        bool hadMoves = false;
        int bestMoveIdx = -1;

        int idxChildOfTreeNode = 0;

        while (generator.GenerateNewMoves(depthRemaining <= 0, currentDepth))
        {
            //Don't use sort because the order is not equal to the order after Predict
            //if (depthRemaining > c_minDepthForSortingMoves)
            //    generator.SortAttackMovesFirst();

            CreateTreeNode(treeNode, generator, currentDepth);

            hadMoves = true;

            for (int i = 0; i < generator.NumMoves; ++i)
            {
                generator.ApplyMoveToBoard(i);

                var nextTreeNode = treeNode.children[idxChildOfTreeNode++];

                int moveScore = CalculateScoreMaximizer((byte)(currentDepth + 1), false, alpha, beta, depthRemaining - 1, quiscenceDepthRemaining - 1, generator.PossibleMoves[i].IsAttack, nextTreeNode);

                if (AbortComputation)// When threaded call this is set if alpha beta pruned this node anyway
                    return bestValue;// Does not atter what we return here

                if (moveScore < bestValue)
                {
                    bestValue = moveScore;
                    bestMoveIdx = idxChildOfTreeNode;

                    if (bestValue < beta)
                    {
                        beta = bestValue;

                        if (beta <= alpha)
                        {
                            generator.RevertMoveFromBoard(i);
                            goto Finish;
                        }
                    }
                }

                generator.RevertMoveFromBoard(i);
            }
        }

        treeNode.max = beta;
        treeNode.min = alpha;
        treeNode.boardScore = m_currentBoardState.BoardScore;

    Finish:

        treeNode.max = beta;
        treeNode.min = alpha;

        m_moveGeneratorsPool.ReturnObject(generatorNode);

        if (storeBestMoveIdx)
            BestMoveIdx = bestMoveIdx;

        if (!hadMoves)// Quiscence or some debug situations
            return m_currentBoardState.BoardScore;
        else
            return bestValue;
    }

    void CreateTreeNode(TreeNode currentTreeNode, AIMovesGenerator generator, int currentDepth)
    {
        foreach (var move in generator.PossibleMoves)
        {
            TreeNode nextTreeNode = new TreeNode();
            nextTreeNode.possibleMove = move;
            nextTreeNode.rootBoard = currentTreeNode.rootBoard;
            nextTreeNode.possibleMovesList = new List<AIMovesGenerator.PossibleMove>(currentTreeNode.possibleMovesList);
            nextTreeNode.possibleMovesList.Add(move);
            nextTreeNode.depth = currentDepth + 1;
            currentTreeNode.children.Add(nextTreeNode);
            numTreeNodes++;
        }
    }
}
