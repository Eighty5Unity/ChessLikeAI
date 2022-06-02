using System.Collections.Generic;

public class TreeNode 
{
    public AIMovesGenerator.PossibleMove possibleMove;
    public List<AIMovesGenerator.PossibleMove> possibleMovesList = new List<AIMovesGenerator.PossibleMove>();
    public List<TreeNode> children = new List<TreeNode>();
    public InitialBoard rootBoard;

    public int depth;
    public int min;
    public int max;
    public int boardScore;

    public string stepFromTo { get {
            var fieldIdxBefore = possibleMove.SourceFieldBefore.FieldIdx;
            var fieldIdxAfter = possibleMove.DestFieldAfter.FieldIdx;
            return
            Constants.BoardLetters[AIBoardState.GetX(fieldIdxBefore)] +
            Constants.BoardNumbers[AIBoardState.GetZ(fieldIdxBefore)] + "->" +
            Constants.BoardLetters[AIBoardState.GetX(fieldIdxAfter)] +
            Constants.BoardNumbers[AIBoardState.GetZ(fieldIdxAfter)]; } }
}
