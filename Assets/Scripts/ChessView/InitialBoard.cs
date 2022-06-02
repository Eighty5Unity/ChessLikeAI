using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialBoard : MonoBehaviour
{
    [SerializeField] GameObject whiteSquarePrefab;
    [SerializeField] GameObject blackSquarePrefab;
    [SerializeField] Transform squareParent;
    [SerializeField] List<BoardPieces> allWhiteChessmanPrefabs;
    [SerializeField] List<BoardPieces> allBlackChessmanPrefabs;
    [SerializeField] NextMovePredictor nextMovePredictor;
    [SerializeField] UIView uiView;
    [SerializeField] List<Chessman> boardPieces;
    [SerializeField] TreeNodeView treeNodeViewPrefab;

    AIBoardState initialBoard;
    GameObject square;
    List<Transform> squares = new List<Transform>();
    List<GameObject> chessmanGameObject = new List<GameObject>();
    TreeNodeView rootTreeNodeView;
    AIMovesGenerator movesGenerator;

    [Serializable]
    public struct Chessman
    {
        [Range(0, Constants.BoardSize - 1)] public int vertical;
        [Range(0, Constants.BoardSize - 1)] public int horizontal;
        public BoardPieces piecePrefab;
    }

    void Start()
    {
        uiView.predictUseThreadingButtonClick += PredictUseThreading;
        uiView.predictWithoutThreadingButtonClick += PredictWithoutThreading;
        uiView.nextMoveButtonClick += NextMoveByClick;

        CreateBoardView();
        CreateBoardState();
        AddViewPiecesOnStart();
        movesGenerator = new AIMovesGenerator();
        movesGenerator.InitializeForBoard(initialBoard);
    }

    void CreateBoardView()
    {
        for(int column = 0; column < Constants.BoardSize; column++)
        {
            for (int line = 0; line < Constants.BoardSize; line++)
            {
                if ((column + line) % 2 == 0)
                {
                    square = Instantiate(blackSquarePrefab, new Vector3(column, line, 0), Quaternion.identity);
                }
                else
                {
                    square = Instantiate(whiteSquarePrefab, new Vector3(column, line, 0), Quaternion.identity);
                }
                square.transform.SetParent(squareParent);
                squares.Add(square.transform);
            }
        }
    }

    void CreateBoardState()
    {
        initialBoard = new AIBoardState();
        initialBoard.Create();
        initialBoard.MovingSide = Constants.ConflictSideWhite;
        initialBoard.MovingSideScoreMul = 1;
        initialBoard.BoardScore = 0;

        foreach (Chessman chessman in boardPieces)
        {
            var chessmanPiece = chessman.piecePrefab;
            AddPieceToBoard(initialBoard, chessman.vertical, chessman.horizontal, (byte)chessmanPiece.conflictSide, chessmanPiece.movementPattern, chessmanPiece.HP, chessmanPiece.firstAttack, chessmanPiece.attack, chessmanPiece.dodge);
        }
    }

    void AddPieceToBoard(AIBoardState currentboard, int x, int z, byte side, MovementPattern movementPattern, int HP, int firstAttack, int attack, float dodge)
    {
        int fieldIdx = AIBoardState.GetFieldIdx(x, z);
        AIBoardState.FieldData field = new AIBoardState.FieldData();
        field.FieldIdx = (byte)fieldIdx;
        field.HasPiece = 1;
        field.PawnLongJumpOverDoneDepth = -100;
        field.Piece.Side = side;
        field.Piece.MovementPattern = (byte)movementPattern;
        field.Piece.HP = (byte)HP;
        field.Piece.Attack = (byte)attack;
        field.Piece.FirstBlowDamage = (byte)firstAttack;
        field.Piece.OneMinusDodgeChance = 1.0f - dodge;
        field.Piece.AliveScore = (byte)Constants.AliveScorePatternMultiplier[(int)movementPattern];
        field.Piece.IsOnStartPosAndNeverMoved = Constants.True;

        currentboard.Board[fieldIdx] = field;
    }

    void AddViewPiecesOnStart()
    {
        foreach(Chessman chessman in boardPieces)
        {
            var chessmanPrefab = Instantiate(chessman.piecePrefab.gameObject, new Vector3(chessman.vertical, chessman.horizontal, 0), Quaternion.identity);
            chessmanPrefab.transform.SetParent(transform);
            chessmanGameObject.Add(chessmanPrefab);
        }
    }

    void PredictUseThreading()
    {
        nextMovePredictor.treeNode = new TreeNode();
        InstantiateRootTreeNodeViewPrefab();
        initialBoard.BoardScore = 0;
        initialBoard.MovingSideScoreMul = 1;
        AIMovesTree.numTreeNodes = 0;
        nextMovePredictor.UseThreading = true;
        nextMovePredictor.Predict(initialBoard);
    }

    void PredictWithoutThreading()
    {
        InstantiateRootTreeNodeViewPrefab();
        nextMovePredictor.UseThreading = false;
        nextMovePredictor.Predict(initialBoard);
    }

    void NextMoveByClick()
    {
        PredictUseThreading();
        StartCoroutine(WaitThenThreadFinishAndMove());
    }

    IEnumerator WaitThenThreadFinishAndMove()
    {
        while (!nextMovePredictor.isThreadFinish)
        {
            yield return null;
        }

        var generator = new AIMovesGenerator();
        generator.InitializeForBoard(initialBoard.Clone());
        generator.GenerateAllMoves(0);
        Move(generator.PossibleMoves[--nextMovePredictor.bestMoveIdx]);
    }

    void OnDestroy()
    {
        uiView.predictUseThreadingButtonClick -= PredictUseThreading;
        uiView.predictWithoutThreadingButtonClick -= PredictWithoutThreading;
        uiView.nextMoveButtonClick -= NextMoveByClick;
    }

    void InstantiateRootTreeNodeViewPrefab()
    {
        rootTreeNodeView = Instantiate(treeNodeViewPrefab);
        rootTreeNodeView.name = "RootTree";
        rootTreeNodeView.treeNode = nextMovePredictor.treeNode;
        rootTreeNodeView.treeNodeViewPrefab = treeNodeViewPrefab;
        rootTreeNodeView.treeNode.rootBoard = this;
        rootTreeNodeView.treeNode.possibleMovesList = new List<AIMovesGenerator.PossibleMove>();
    }

    public void MovePiecesOnBoardView(List<AIMovesGenerator.PossibleMove> possibleMoves, AIMovesGenerator.PossibleMove lastMove)
    {
        AIMovesGenerator movesGenerator = new AIMovesGenerator();
        var cloneBoard = initialBoard.Clone();
        movesGenerator.InitializeForBoard(cloneBoard);

        foreach (var move in possibleMoves)
        {
            movesGenerator.ApplyMoveToBoard(move);
        }

        ReloadBoardView(cloneBoard, lastMove);
    }

    public void Move(AIMovesGenerator.PossibleMove move)
    {
        movesGenerator.ApplyMoveToBoard(move);
        ReloadBoardView(initialBoard, move);
        CheckLongJumpField(initialBoard.Board);
        Destroy(rootTreeNodeView.gameObject);
    }

    void ReloadBoardView(AIBoardState board, AIMovesGenerator.PossibleMove move)
    {
        foreach (var chessman in chessmanGameObject)
        {
            Destroy(chessman.gameObject);
        }

        foreach(var piece in board.Board)
        {
            if(piece.HasPiece == Constants.True)
            {
                var side = piece.Piece.Side;
                var positionX = AIBoardState.GetX(piece.FieldIdx);
                var positionY = AIBoardState.GetZ(piece.FieldIdx);

                if(side == Constants.ConflictSideWhite)
                {
                    InstatiateChessman(allWhiteChessmanPrefabs, piece.Piece, positionX, positionY);
                }
                else if(side == Constants.ConflictSideBlack)
                {
                    InstatiateChessman(allBlackChessmanPrefabs, piece.Piece, positionX, positionY);
                }
            }
        }

        var ghostSide = move.SourceFieldBefore.Piece.Side;

        if (ghostSide == Constants.ConflictSideWhite)
        {
            InstatiateChessmanGhost(allWhiteChessmanPrefabs, move);
        }
        else if(ghostSide == Constants.ConflictSideBlack)
        {
            InstatiateChessmanGhost(allBlackChessmanPrefabs, move);
        }
    }

    void InstatiateChessman(List<BoardPieces> allChessmanPrefab, AIBoardState.PieceData piece, int positionX, int positionY)
    {
        var movementPattern = (MovementPattern)piece.MovementPattern;

        foreach(var chessman in allChessmanPrefab)
        {
            if(chessman.movementPattern == movementPattern)
            {
                var chessmanPrefab = Instantiate(chessman.gameObject, new Vector3(positionX, positionY, 0), Quaternion.identity);
                chessmanPrefab.transform.SetParent(transform);

                var chessmanInfo = chessmanPrefab.GetComponent<BoardPieces>();
                chessmanInfo.HP = piece.HP;
                chessmanInfo.firstAttack = piece.FirstBlowDamage;
                chessmanInfo.attack = piece.Attack;
                chessmanInfo.dodge = piece.OneMinusDodgeChance;
                chessmanInfo.neverMoved = piece.IsOnStartPosAndNeverMoved;

                chessmanGameObject.Add(chessmanPrefab);
            }
        }
    }

    void InstatiateChessmanGhost(List<BoardPieces> allChessmanPrefab, AIMovesGenerator.PossibleMove move)
    {
        var ghostPositionX = AIBoardState.GetX(move.SourceFieldBefore.FieldIdx);
        var ghostPositionY = AIBoardState.GetZ(move.SourceFieldBefore.FieldIdx);
        var ghostType = (MovementPattern)move.SourceFieldBefore.Piece.MovementPattern;

        foreach (var chessmanGhost in allChessmanPrefab)
        {
            if(chessmanGhost.movementPattern == ghostType)
            {
                var chessmanPrefabGhost = Instantiate(chessmanGhost.gameObject, new Vector3(ghostPositionX, ghostPositionY, 0), Quaternion.identity);
                chessmanPrefabGhost.transform.SetParent(transform);
                chessmanPrefabGhost.name = "Ghost";
                var sprite = chessmanPrefabGhost.GetComponent<SpriteRenderer>();
                var color = sprite.color;
                color.a = 0.3f;
                sprite.color = color;

                var boardPiecesGhost = chessmanPrefabGhost.GetComponent<BoardPieces>();
                boardPiecesGhost.isGhost = true;

                chessmanGameObject.Add(chessmanPrefabGhost);
            }
        }
    }

    void CheckLongJumpField(AIBoardState.FieldData[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].PawnLongJumpOverDoneDepth == -1)
            {
                fields[i].PawnLongJumpOverDoneDepth = -100;
            }
            else if (fields[i].PawnLongJumpOverDoneDepth == 0)
            {
                fields[i].PawnLongJumpOverDoneDepth = -1;
            }
        }
    }
}
