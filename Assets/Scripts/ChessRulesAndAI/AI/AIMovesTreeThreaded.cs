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
public class AIMovesTreeThreaded : AIMovesTreeWithScore
{
    public class ThreadWithTree
    {
        public Thread Thread;
        public AIMovesTreeWithScore Tree;
        public int CreatedForMoveIdx;
    }

    const int c_maxThreads = 3;

    public static ThreadWithTree CalculateScoreOnSingleThread(bool maximizer, AIBoardState currentBoardState, int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, byte wasLastMoveAnAttack, TreeNode treeNode)
    {
        ThreadWithTree returnThread = new ThreadWithTree();
        AIMovesTree movesTree = new AIMovesTree();
        returnThread.Tree = movesTree;
        movesTree.Create(currentBoardState);

        if (maximizer)
            returnThread.Thread = new Thread(() => movesTree.CalculateScoreMaximizerStoreVal(alpha, beta, depthRemaining, quiscenceDepthRemaining, wasLastMoveAnAttack, treeNode));
        else
            returnThread.Thread = new Thread(() => movesTree.CalculateScoreMinimizerStoreVal(alpha, beta, depthRemaining, quiscenceDepthRemaining, wasLastMoveAnAttack, treeNode));

        returnThread.Thread.Start();
        return returnThread;
    }

    public static ThreadWithTree CalculateScoreWithThreadingChildren(bool maximizer, AIBoardState currentBoardState, int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, bool wasLastMoveAnAttack)
    {
        ThreadWithTree returnThread = new ThreadWithTree();
        AIMovesTreeThreaded tree = new AIMovesTreeThreaded();

        returnThread.Thread = new Thread(() => tree.CalculateScoreThreads(maximizer, currentBoardState, alpha, beta, depthRemaining, quiscenceDepthRemaining, wasLastMoveAnAttack));
        returnThread.Tree = tree;

        returnThread.Thread.Start();
        return returnThread;
    }

    protected void CalculateScoreThreads(bool maximizer, AIBoardState startingBoard, int alpha, int beta, int depthRemaining, int quiscenceDepthRemaining, bool wasLastMoveAnAttack)
    {
        AIBoardState currentBoard = startingBoard.Clone();
        AIMovesGenerator generator = new AIMovesGenerator();
        generator.InitializeForBoard(currentBoard);

        int bestValue = int.MaxValue;

        if (maximizer)
            bestValue = int.MinValue;

        generator.GenerateAllMoves(0);
        generator.SortAttackMovesFirst();

        int processorCount = 8;
        List<ThreadWithTree> activeThreads = new List<ThreadWithTree>(processorCount);


        for (int i = 0; i < generator.NumMoves; ++i)
        {
            int maxThreads = Mathf.Min(i / 2 + 1, processorCount);// So we do not spawn threads if the node is "obviously wrong or good", empirically adjusted

            //if (splitFirstChildToThreads)
            //    maxThreads = 1;

            generator.ApplyMoveToBoard(i);

            ThreadWithTree newThread;

            //if (splitFirstChildToThreads)// && (i == 0))// Only in this case it makes sense
            //    newThread = CalculateScoreWithThreadingChildren(false, !maximizer, currentBoard, alpha, beta, depthRemaining - 1, quiscenceDepthRemaining - 1, generator.PossibleMoves[i].IsAttack);
            //else
                newThread = CalculateScoreOnSingleThread(!maximizer, currentBoard, alpha, beta, depthRemaining - 1, quiscenceDepthRemaining - 1, generator.PossibleMoves[i].IsAttack, new TreeNode());

            newThread.CreatedForMoveIdx = i;
            activeThreads.Add(newThread);
            
            generator.RevertMoveFromBoard(i);

            while ((activeThreads.Count >= maxThreads) || ((i == generator.NumMoves - 1) && (activeThreads.Count > 0)))
            {
                Thread.Sleep(10);

                for (int threadIdx = 0; threadIdx < activeThreads.Count; ++threadIdx)
                {
                    if (!activeThreads[threadIdx].Thread.IsAlive)
                    {
                        int moveScore = activeThreads[threadIdx].Tree.LastScoreVal;

                        if (maximizer)
                        {
                            if (moveScore > bestValue)
                            {
                                bestValue = moveScore;
                                BestMoveIdx = activeThreads[threadIdx].CreatedForMoveIdx;

                                if (bestValue > alpha)
                                {
                                    alpha = bestValue;

                                    if (beta <= alpha)
                                    {
                                        goto Finish;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (moveScore < bestValue)
                            {
                                bestValue = moveScore;
                                BestMoveIdx = activeThreads[threadIdx].CreatedForMoveIdx;

                                if (bestValue < beta)
                                {
                                    beta = bestValue;

                                    if (beta <= alpha)
                                        goto Finish;
                                }

                            }
                        }

                        activeThreads.RemoveAt(threadIdx);
                        --threadIdx;

                    }
                }
            }
        }

Finish:
        for (int i = 0; i < activeThreads.Count; ++i)
            activeThreads[i].Tree.AbortComputation = true;

        //If pruned wait for all the threads to finish
        while (activeThreads.Count > 0)
        {
            for (int i = 0; i < activeThreads.Count; ++i)
            {
                if (!activeThreads[i].Thread.IsAlive)
                {
                    activeThreads.RemoveAt(i);
                    --i;
                }
            }
        }

        LastScoreVal = bestValue;
    }
}
