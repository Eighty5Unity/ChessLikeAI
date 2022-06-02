using UnityEngine;

public class TreeNodeView : MonoBehaviour
{
    bool showJustOnce = true;
    public TreeNode treeNode;
    [HideInInspector] public TreeNodeView treeNodeViewPrefab;

    [Header("Parameters")]
    public int currentDepth;
    public int max;
    public int min;
    public int scoreBeforeMove;
    public int scoreAfterMove;
    public int boardScore;
    public bool isAttack;

    public void Initialize(TreeNode treeNodeValue, TreeNodeView prefab, int numberOfChild)
    {
        treeNode = treeNodeValue;
        treeNodeViewPrefab = prefab;

        currentDepth = treeNode.depth;
        max = treeNode.max;
        min = treeNode.min;
        boardScore = treeNode.boardScore;
        scoreBeforeMove = treeNode.possibleMove.ScoreBeforeMove;
        scoreAfterMove = treeNode.possibleMove.ScoreAfterMove;
        isAttack = treeNode.possibleMove.IsAttack == 1 ? true : false;
        name = "(" + ++numberOfChild + ")  " + treeNode.stepFromTo + " Score: " + scoreAfterMove;
    }

    public void InstantiateTreeNode()
    {
        if (showJustOnce)
        {
            showJustOnce = false;

            for (int i = 0; i < treeNode.children.Count; i++)
            {
                var treeNodePrefab = Instantiate(treeNodeViewPrefab, this.transform);
                treeNodePrefab.Initialize(treeNode.children[i], treeNodeViewPrefab, i);
            }
        }
    }

    public void MoveChessmanView()
    {
        treeNode.rootBoard.MovePiecesOnBoardView(treeNode.possibleMovesList, treeNode.possibleMove);
    }

    public void Move()
    {
        treeNode.rootBoard.Move(treeNode.possibleMove);
    }
}
