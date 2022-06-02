using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using System;
using Unity.Burst;
using System.Runtime.CompilerServices;

[BurstCompile(CompileSynchronously = true)]
public class AIMovesGenerator
{
    public static int s_numGeneratedMoves;
    public static int s_numGeneratedAttacks;

    public class PossibleMove
    {
        public enum SpecialMoveType
        {
            None = 0,
            LongJump = 1,
            BeatInPassing = 2,
            Castle = 3
        }
        public virtual SpecialMoveType MoveType { get { return SpecialMoveType.None; } }

        public byte IsAttack;

        public int ScoreAfterMove;
        public int ScoreBeforeMove;

        public AIBoardState.FieldData DestFieldAfter;
        public AIBoardState.FieldData DestFieldBefore;
        public AIBoardState.FieldData SourceFieldBefore;
    }

    public class PossibleMoveSpecial : PossibleMove
    {


        public static void CopyFromSingleMove(PossibleMove move, PossibleMoveSpecial spcialMove)
        {
            spcialMove.IsAttack = move.IsAttack;
            spcialMove.ScoreAfterMove = move.ScoreAfterMove;
            spcialMove.ScoreBeforeMove = move.ScoreBeforeMove;
            spcialMove.DestFieldAfter = move.DestFieldAfter;
            spcialMove.DestFieldBefore = move.DestFieldBefore;
            spcialMove.SourceFieldBefore = move.SourceFieldBefore;
        }
    }

    public class PossibleMovePawnLongjump : PossibleMoveSpecial
    {
        public override SpecialMoveType MoveType { get { return SpecialMoveType.LongJump; } }
        public AIBoardState.FieldData JumpedOverFieldAfter;
        public AIBoardState.FieldData JumpedOverFieldBefore;
    }

    public class PossibleMoveBeatingInPassing : PossibleMoveSpecial
    {
        public override SpecialMoveType MoveType { get { return SpecialMoveType.BeatInPassing; } }
        public AIBoardState.FieldData DefenderFieldAfter;
        public AIBoardState.FieldData DefenderFieldBefore;
    }

    public class PossibleMoveCastle : PossibleMoveSpecial
    {
        public override SpecialMoveType MoveType { get { return SpecialMoveType.Castle; } }
        public AIBoardState.FieldData DestFieldAfterRook;
        public AIBoardState.FieldData DestFieldBeforeRook;
        public AIBoardState.FieldData SourceFieldBeforeRook;
    }

    AIBoardState m_boardState = null;

    int positiveScore = 20; //attack always gives positive score

    public List<PossibleMove> PossibleMoves;
    AIBoardState.FieldData EmptyFieldCached;

    int m_processedFieldIdx;
    public int NumMoves { get { return PossibleMoves.Count; } }

    public AIMovesGenerator()
    {
        PossibleMoves = new List<PossibleMove>(128);// Empirically adjsed
        EmptyFieldCached.HasPiece = 0;
    }

    public void InitializeForBoard(AIBoardState boardState)
    {
        PossibleMoves.Clear();
        m_boardState = boardState;
        m_processedFieldIdx = 0;
    }

    public void GenerateAllMoves(byte currentDepth)
    {
        for (int i = 0; i < m_boardState.Board.Length; ++i)
        {
            if ((m_boardState.Board[i].HasPiece == Constants.True) && (m_boardState.Board[i].Piece.Side == m_boardState.MovingSide))
                GenerateMoveForPiece(i, m_boardState.Board[i].Piece.MovementPattern, false, currentDepth);
        }
    }

    public void SortAttackMovesFirst()
    {
        int numSwappedAttacks = 0;

        for (int i = 0; i < PossibleMoves.Count; ++i)
        {
            if (PossibleMoves[i].IsAttack == Constants.True)
            {
                for (int z = numSwappedAttacks; z < i; ++z)
                {
                    if (PossibleMoves[z].IsAttack == Constants.False)
                    {
                        PossibleMove temp = PossibleMoves[z];
                        PossibleMoves[z] = PossibleMoves[i];
                        PossibleMoves[i] = temp;
                        break;
                    }
                }

                ++numSwappedAttacks;
            }
        }
    }

    public bool GenerateNewMoves(bool attacksOnly, byte currentDepth)
    {
        PossibleMoves.Clear();

        /*
        if (m_moveGenerationStepIdx > 0)
            return false;

        ++m_moveGenerationStepIdx;
        */

        for (; m_processedFieldIdx < m_boardState.Board.Length; ++m_processedFieldIdx)
        {
            if ((m_boardState.Board[m_processedFieldIdx].HasPiece == Constants.True) && (m_boardState.Board[m_processedFieldIdx].Piece.Side == m_boardState.MovingSide))
            {
                GenerateMoveForPiece(m_processedFieldIdx, m_boardState.Board[m_processedFieldIdx].Piece.MovementPattern, attacksOnly, currentDepth);

                if (PossibleMoves.Count > 0)
                {
                    ++m_processedFieldIdx;
                    return true;
                }
            }

        }

        return PossibleMoves.Count > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyMoveToBoard(int idx)
    {
        ApplyMoveToBoard(PossibleMoves[idx]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyMoveToBoard(PossibleMove moveToApply)
    {
        m_boardState.Board[moveToApply.DestFieldAfter.FieldIdx] = moveToApply.DestFieldAfter;

        EmptyFieldCached.FieldIdx = moveToApply.SourceFieldBefore.FieldIdx;
        EmptyFieldCached.PawnLongJumpOverDoneDepth = -100;
        m_boardState.Board[moveToApply.SourceFieldBefore.FieldIdx] = EmptyFieldCached;
        m_boardState.BoardScore = moveToApply.ScoreAfterMove;
        m_boardState.MovingSide = (m_boardState.MovingSide == Constants.ConflictSideWhite) ? Constants.ConflictSideBlack : Constants.ConflictSideWhite;
        m_boardState.MovingSideScoreMul = -m_boardState.MovingSideScoreMul;

        if (moveToApply.MoveType != PossibleMove.SpecialMoveType.None)
        {
            if (moveToApply.MoveType == PossibleMove.SpecialMoveType.Castle)
                ApplyRemainingSpecialMoveCastleToBoard((PossibleMoveCastle)moveToApply);
            else if (moveToApply.MoveType == PossibleMove.SpecialMoveType.BeatInPassing)
                ApplyRemainingSpecialMoveBeatInPassing((PossibleMoveBeatingInPassing)moveToApply);
            else if (moveToApply.MoveType == PossibleMove.SpecialMoveType.LongJump)
                ApplyRemainingSpecialMovePawnLongJump((PossibleMovePawnLongjump)moveToApply);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ApplyRemainingSpecialMoveCastleToBoard(PossibleMoveCastle possibleMove)
    {
        EmptyFieldCached.FieldIdx = possibleMove.SourceFieldBeforeRook.FieldIdx;
        m_boardState.Board[possibleMove.SourceFieldBeforeRook.FieldIdx] = EmptyFieldCached;

        m_boardState.Board[possibleMove.DestFieldAfterRook.FieldIdx] = possibleMove.DestFieldAfterRook;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ApplyRemainingSpecialMoveBeatInPassing(PossibleMoveBeatingInPassing possibleMove)
    {
        m_boardState.Board[possibleMove.DefenderFieldAfter.FieldIdx] = possibleMove.DefenderFieldAfter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ApplyRemainingSpecialMovePawnLongJump(PossibleMovePawnLongjump possibleMove)
    {
        m_boardState.Board[possibleMove.JumpedOverFieldAfter.FieldIdx] = possibleMove.JumpedOverFieldAfter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetMoveScore(int idx)
    {
        return PossibleMoves[idx].ScoreAfterMove;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RevertMoveFromBoard(int idx)
    {
        PossibleMove moveToApply = PossibleMoves[idx];
        m_boardState.Board[moveToApply.DestFieldAfter.FieldIdx] = moveToApply.DestFieldBefore;
        m_boardState.Board[moveToApply.SourceFieldBefore.FieldIdx] = moveToApply.SourceFieldBefore;

        m_boardState.BoardScore = moveToApply.ScoreBeforeMove;

        m_boardState.MovingSide = (m_boardState.MovingSide == Constants.ConflictSideWhite) ? Constants.ConflictSideBlack : Constants.ConflictSideWhite;
        m_boardState.MovingSideScoreMul = -m_boardState.MovingSideScoreMul;

        if (moveToApply.MoveType != PossibleMove.SpecialMoveType.None)
        {
            if (moveToApply.MoveType == PossibleMove.SpecialMoveType.Castle)
                RevertRemainingSpecialMoveCastleToBoard((PossibleMoveCastle)moveToApply);
            else if (moveToApply.MoveType == PossibleMove.SpecialMoveType.BeatInPassing)
                RevertRemainingSpecialMoveBeatInPassing((PossibleMoveBeatingInPassing)moveToApply);
            else if (moveToApply.MoveType == PossibleMove.SpecialMoveType.LongJump)
                RevertRemainingSpecialMovePawnLongJump((PossibleMovePawnLongjump)moveToApply);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RevertRemainingSpecialMoveCastleToBoard(PossibleMoveCastle possibleMove)
    {
        m_boardState.Board[possibleMove.SourceFieldBeforeRook.FieldIdx] = possibleMove.SourceFieldBeforeRook;
        m_boardState.Board[possibleMove.DestFieldBeforeRook.FieldIdx] = possibleMove.DestFieldBeforeRook;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RevertRemainingSpecialMoveBeatInPassing(PossibleMoveBeatingInPassing possibleMove)
    {
        m_boardState.Board[possibleMove.DefenderFieldBefore.FieldIdx] = possibleMove.DefenderFieldBefore;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RevertRemainingSpecialMovePawnLongJump(PossibleMovePawnLongjump possibleMove)
    {
        m_boardState.Board[possibleMove.JumpedOverFieldBefore.FieldIdx] = possibleMove.JumpedOverFieldBefore;
    }

    public void GenerateMoveForPiece(int fieldIdx, byte movementPattern, bool attacksOnly, byte currentDepth)
    {
        if (movementPattern == Constants.MovementPatternPawn)
            GenerateMovesForPawn(fieldIdx, attacksOnly, currentDepth);
        else if (movementPattern == Constants.MovementPatternRook)
            GenerateMovesForRook(fieldIdx, attacksOnly);
        else if (movementPattern == Constants.MovementPatternBishop)
            GenerateMovesForBishop(fieldIdx, attacksOnly);
        else if (movementPattern == Constants.MovementPatternKnight)
            GenerateMovesForKnight(fieldIdx, attacksOnly);
        else if (movementPattern == Constants.MovementPatternQueen)
            GenerateMovesForQueen(fieldIdx, attacksOnly);
        else if (movementPattern == Constants.MovementPatternKing)
            GenerateMovesForKing(fieldIdx, attacksOnly);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMovesForPawn(int sourceFieldIdx, bool attacksOnly, byte currMoveDepth)
    {
        int x = AIBoardState.GetX(sourceFieldIdx);
        int z = AIBoardState.GetZ(sourceFieldIdx);

        int moveDir = (m_boardState.Board[sourceFieldIdx].Piece.Side == Constants.ConflictSideWhite ? 1 : -1);
        int aheadZ = z + moveDir;

        int attackFieldIdx1 = AIBoardState.GetFieldIdx(x - 1, aheadZ);
        int attackFieldIdx2 = AIBoardState.GetFieldIdx(x + 1, aheadZ);

        if (AIBoardState.IsOnBoard(x - 1, z + moveDir))
        {
            AddMoveIfPossible(sourceFieldIdx, attackFieldIdx1, true);// Attacks only
            AddBeatingInPassingMoveIfPossible(x - 1, z + moveDir, x, z, currMoveDepth);
        }


        if (AIBoardState.IsOnBoard(x + 1, z + moveDir))
        {
            AddMoveIfPossible(sourceFieldIdx, attackFieldIdx2, true);// Attacks only
            AddBeatingInPassingMoveIfPossible(x + 1, z + moveDir, x, z, currMoveDepth);
        }

        if (!attacksOnly)
        {
            int aheadFieldIdx = AIBoardState.GetFieldIdx(x, aheadZ);

            if (AIBoardState.IsOnBoard(x, aheadZ) && (m_boardState.Board[aheadFieldIdx].HasPiece == Constants.False))// We can only do move forward so check for it here
            {
                AddMoveIfPossible(sourceFieldIdx, aheadFieldIdx, attacksOnly);// Already checked that we can move there

                if (((aheadZ == 0) && (m_boardState.Board[sourceFieldIdx].Piece.Side == Constants.ConflictSideBlack)) || ((aheadZ == 7) && (m_boardState.Board[sourceFieldIdx].Piece.Side == Constants.ConflictSideWhite)))
                    PromotePawnFromLastMove();

                if (m_boardState.Board[sourceFieldIdx].Piece.IsOnStartPosAndNeverMoved == Constants.True)
                {
                    int aheadFieldIdx2 = AIBoardState.GetFieldIdx(x, aheadZ + moveDir);
                    if (m_boardState.Board[aheadFieldIdx2].HasPiece == Constants.False)// We can only do move so check for it here
                    {
                        AddMoveIfPossible(sourceFieldIdx, aheadFieldIdx2, attacksOnly);
                        PossibleMovePawnLongjump specialMove = new PossibleMovePawnLongjump();
                        PossibleMoveSpecial.CopyFromSingleMove(PossibleMoves[PossibleMoves.Count - 1], specialMove);
                        specialMove.JumpedOverFieldBefore = m_boardState.Board[aheadFieldIdx];
                        specialMove.JumpedOverFieldAfter = m_boardState.Board[aheadFieldIdx];
                        specialMove.JumpedOverFieldAfter.PawnLongJumpOverDoneDepth = (sbyte)currMoveDepth;
                        PossibleMoves[PossibleMoves.Count - 1] = specialMove;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void PromotePawnFromLastMove()
    {
        int lastMoveIdx = PossibleMoves.Count - 1;

        // Subtract score for this piece - assume we are an ally
        int scoreChange = -GetLostHPScore(PossibleMoves[lastMoveIdx].DestFieldAfter.Piece, PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.HP);
        scoreChange -= PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.AliveScore;

        PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.MovementPattern = Constants.MovementPatternQueen;
        PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.Attack *= 2;
        PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.FirstBlowDamage *= 2;
        PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.HP *= 2;
        PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.AliveScore *= 2;

        // Add score for new piece, assume we are an ally
        scoreChange += PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.AliveScore;
        scoreChange += GetLostHPScore(PossibleMoves[lastMoveIdx].DestFieldAfter.Piece, PossibleMoves[lastMoveIdx].DestFieldAfter.Piece.HP);
        scoreChange *= m_boardState.MovingSideScoreMul;// Multiply by conflict side now

        PossibleMoves[lastMoveIdx].ScoreAfterMove = m_boardState.BoardScore + scoreChange;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMovesForKnight(int sourceFieldIdx, bool attacksOnly)
    {
        int x = AIBoardState.GetX(sourceFieldIdx);
        int z = AIBoardState.GetZ(sourceFieldIdx);

        if (AIBoardState.IsOnBoard(x - 1, z + 2))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z + 2), attacksOnly);

        if (AIBoardState.IsOnBoard(x + 1, z + 2))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z + 2), attacksOnly);

        if (AIBoardState.IsOnBoard(x - 1, z - 2))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z - 2), attacksOnly);

        if (AIBoardState.IsOnBoard(x + 1, z - 2))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z - 2), attacksOnly);


        if (AIBoardState.IsOnBoard(x - 2, z + 1))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 2, z + 1), attacksOnly);

        if (AIBoardState.IsOnBoard(x + 2, z + 1))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 2, z + 1), attacksOnly);

        if (AIBoardState.IsOnBoard(x - 2, z - 1))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 2, z - 1), attacksOnly);

        if (AIBoardState.IsOnBoard(x + 2, z - 1))
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 2, z - 1), attacksOnly);

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMovesForRook(int sourceFieldIdx, bool attacksOnly)
    {
        int posX = AIBoardState.GetX(sourceFieldIdx);
        int posZ = AIBoardState.GetZ(sourceFieldIdx);

        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, 0, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, 0, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 0, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 0, -1, attacksOnly);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMovesForBishop(int sourceFieldIdx, bool attacksOnly)
    {
        int posX = AIBoardState.GetX(sourceFieldIdx);
        int posZ = AIBoardState.GetZ(sourceFieldIdx);

        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, -1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, -1, attacksOnly);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMovesForQueen(int sourceFieldIdx, bool attacksOnly)
    {
        int posX = AIBoardState.GetX(sourceFieldIdx);
        int posZ = AIBoardState.GetZ(sourceFieldIdx);

        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, 0, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, 0, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 0, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 0, -1, attacksOnly);

        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, 1, -1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, 1, attacksOnly);
        GenerateMovesInLine(sourceFieldIdx, posX, posZ, -1, -1, attacksOnly);
    }

    public void GenerateMovesForKing(int sourceFieldIdx, bool attacksOnly)
    {
        int x = AIBoardState.GetX(sourceFieldIdx);
        int z = AIBoardState.GetZ(sourceFieldIdx);

        if ((x > 0) && (z > 0) && (x < Constants.BoardSize - 1) && (z < Constants.BoardSize - 1))
        {
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z), attacksOnly);
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z - 1), attacksOnly);
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z + 1), attacksOnly);


            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z), attacksOnly);
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z - 1), attacksOnly);
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z + 1), attacksOnly);

            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x, z - 1), attacksOnly);
            AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x, z + 1), attacksOnly);

        }
        else
        {
            if (AIBoardState.IsOnBoard(x - 1, z))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z), attacksOnly);

            if (AIBoardState.IsOnBoard(x - 1, z - 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z - 1), attacksOnly);

            if (AIBoardState.IsOnBoard(x - 1, z + 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x - 1, z + 1), attacksOnly);

            if (AIBoardState.IsOnBoard(x + 1, z))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z), attacksOnly);

            if (AIBoardState.IsOnBoard(x + 1, z - 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z - 1), attacksOnly);

            if (AIBoardState.IsOnBoard(x + 1, z + 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x + 1, z + 1), attacksOnly);

            if (AIBoardState.IsOnBoard(x, z - 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x, z - 1), attacksOnly);

            if (AIBoardState.IsOnBoard(x, z + 1))
                AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(x, z + 1), attacksOnly);

            if (m_boardState.Board[sourceFieldIdx].Piece.IsOnStartPosAndNeverMoved == Constants.True)
            {
                AddCastleIfPossible(x, z, 7);
                AddCastleIfPossible(x, z, 0);
            }
        }
    }

    public void GenerateMovesInLine(int sourceFieldIdx, int posX, int posZ, int changeX, int changeZ, bool attacksOnly)
    {
        int nextPosX = posX + changeX;
        int nextPosZ = posZ + changeZ;

        while (AIBoardState.IsOnBoard(nextPosX, nextPosZ) && AddMoveIfPossible(sourceFieldIdx, AIBoardState.GetFieldIdx(nextPosX, nextPosZ), attacksOnly))
        {
            nextPosX = nextPosX + changeX;
            nextPosZ = nextPosZ + changeZ;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddCastleIfPossible(int sourceX, int sourceZ, int rookX)
    {
        //Check if rook has moved
        int rookFieldIdx = AIBoardState.GetFieldIdx(rookX, sourceZ);
        if ((m_boardState.Board[rookFieldIdx].HasPiece == Constants.False) || (m_boardState.Board[rookFieldIdx].Piece.MovementPattern != Constants.MovementPatternRook) || (m_boardState.Board[rookFieldIdx].Piece.IsOnStartPosAndNeverMoved == Constants.False))
            return;

        // Check if there is a clear path from king to the rook
        int dir = rookX > sourceX ? 1 : -1;
        for (int x = sourceX + dir; x != rookX; x += dir)
        {
            if (m_boardState.Board[AIBoardState.GetFieldIdx(x, sourceZ)].HasPiece == Constants.True)
                return;
        }

        int kingFieldIdx = AIBoardState.GetFieldIdx(sourceX, sourceZ);

        // Move is possible so 
        PossibleMoveCastle move = new PossibleMoveCastle();
        move.ScoreBeforeMove = m_boardState.BoardScore;
        move.SourceFieldBefore = m_boardState.Board[kingFieldIdx];
        move.SourceFieldBeforeRook = m_boardState.Board[rookFieldIdx];
        move.IsAttack = Constants.False;

        move.ScoreAfterMove = m_boardState.BoardScore + 20;//??

        int kingMoveDestIdx = AIBoardState.GetFieldIdx(rookX == 7? 6: 1, sourceZ);
        int rookMoveDestIdx = AIBoardState.GetFieldIdx(rookX == 7 ? 5 : 2, sourceZ);

        move.DestFieldBefore = m_boardState.Board[kingMoveDestIdx];
        move.DestFieldBeforeRook = m_boardState.Board[rookMoveDestIdx];

        move.DestFieldAfter = m_boardState.Board[kingMoveDestIdx];
        move.DestFieldAfterRook = m_boardState.Board[rookMoveDestIdx];

        move.DestFieldAfter.HasPiece = Constants.True;
        move.DestFieldAfterRook.HasPiece = Constants.True;

        move.DestFieldAfter.Piece = m_boardState.Board[kingFieldIdx].Piece;
        move.DestFieldAfterRook.Piece = m_boardState.Board[rookFieldIdx].Piece;

        move.DestFieldAfter.Piece.IsOnStartPosAndNeverMoved = Constants.False;
        move.DestFieldAfterRook.Piece.IsOnStartPosAndNeverMoved = Constants.False;
        ++s_numGeneratedMoves;
        PossibleMoves.Add(move);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddBeatingInPassingMoveIfPossible(int destX, int destZ, int sourceX, int sourceZ, int currDepth)
    {
        int attackerFieldIdx;
        int defenderFieldIdx;
        int moveToFieldIdx;
        if (destZ == 2)
        {
            moveToFieldIdx = AIBoardState.GetFieldIdx(destX, destZ);

            if (m_boardState.Board[moveToFieldIdx].PawnLongJumpOverDoneDepth != (currDepth - 1))
                return;

            attackerFieldIdx = AIBoardState.GetFieldIdx(sourceX, sourceZ);

            if (m_boardState.Board[attackerFieldIdx].Piece.Side != Constants.ConflictSideBlack)
                return;

            defenderFieldIdx = AIBoardState.GetFieldIdx(destX, 3);
        }
        else if (destZ == 5)
        {
            moveToFieldIdx = AIBoardState.GetFieldIdx(destX, destZ);

            if (m_boardState.Board[moveToFieldIdx].PawnLongJumpOverDoneDepth != (currDepth - 1))
                return;

            attackerFieldIdx = AIBoardState.GetFieldIdx(sourceX, sourceZ);

            if (m_boardState.Board[attackerFieldIdx].Piece.Side != Constants.ConflictSideWhite)
                return;

            defenderFieldIdx = AIBoardState.GetFieldIdx(destX, 4);
        }
        else
            return;

        PossibleMoveBeatingInPassing specialMove = new PossibleMoveBeatingInPassing();
        specialMove.IsAttack = Constants.True;
        specialMove.ScoreBeforeMove = m_boardState.BoardScore;
        specialMove.SourceFieldBefore = m_boardState.Board[attackerFieldIdx];
        specialMove.DestFieldBefore = m_boardState.Board[moveToFieldIdx];
        specialMove.DestFieldAfter = m_boardState.Board[moveToFieldIdx];
        
        specialMove.DefenderFieldBefore = m_boardState.Board[defenderFieldIdx];
        specialMove.DefenderFieldAfter = m_boardState.Board[defenderFieldIdx];


        bool attackerWon = CalculateAttackResult(specialMove, m_boardState.Board[attackerFieldIdx].Piece, m_boardState.Board[defenderFieldIdx].Piece);

        if (attackerWon)
        {
            specialMove.DefenderFieldAfter.HasPiece = Constants.False;
            specialMove.DestFieldAfter.HasPiece = Constants.True;
        }
        else
        {
            specialMove.DefenderFieldAfter.Piece = specialMove.DestFieldBefore.Piece;// The resulting defender was written to the wrong field using this function
            specialMove.DestFieldAfter.HasPiece = Constants.False;// Just in case
        }

        ++s_numGeneratedAttacks;
        PossibleMoves.Add(specialMove);
    }

    // Returns if move not possible or attack (so if move in this direction for line moves still possible)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool AddMoveIfPossible(int sourceFieldIdx, int destFieldIdx, bool attacksOnly)
    {
        if (m_boardState.Board[destFieldIdx].HasPiece == Constants.True)// Field is occupied
        {
            if (m_boardState.Board[destFieldIdx].Piece.Side == m_boardState.MovingSide)
                return false;

            PossibleMove possibleMove = new PossibleMove();

            possibleMove.DestFieldBefore = m_boardState.Board[destFieldIdx];
            possibleMove.DestFieldAfter = m_boardState.Board[destFieldIdx];
            possibleMove.SourceFieldBefore = m_boardState.Board[sourceFieldIdx];
            possibleMove.ScoreBeforeMove = m_boardState.BoardScore;
            possibleMove.IsAttack = Constants.True;
            CalculateAttackResult(possibleMove, m_boardState.Board[sourceFieldIdx].Piece, possibleMove.DestFieldBefore.Piece);

            PossibleMoves.Add(possibleMove);
            ++s_numGeneratedAttacks;

            return false;
        }
        else if (!attacksOnly)
        {
            PossibleMove possibleMove = new PossibleMove();

            possibleMove.DestFieldBefore = m_boardState.Board[destFieldIdx];
            possibleMove.DestFieldAfter = m_boardState.Board[destFieldIdx];
            possibleMove.SourceFieldBefore = m_boardState.Board[sourceFieldIdx];
            possibleMove.ScoreBeforeMove = m_boardState.BoardScore;

            possibleMove.IsAttack = Constants.False;
            possibleMove.ScoreAfterMove = m_boardState.BoardScore;

            possibleMove.DestFieldAfter.HasPiece = 1;
            possibleMove.DestFieldAfter.Piece = m_boardState.Board[sourceFieldIdx].Piece;
            possibleMove.DestFieldAfter.Piece.IsOnStartPosAndNeverMoved = Constants.False;

            PossibleMoves.Add(possibleMove);
            ++s_numGeneratedMoves;

            return true;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // Retunrs if attacker won
    bool CalculateAttackResult(PossibleMove possibleMove, AIBoardState.PieceData attacker, AIBoardState.PieceData defender)
    {
        float damagePerAttackAttacker = attacker.Attack * defender.OneMinusDodgeChance;
        float damagePerAttackDefender = defender.Attack * attacker.OneMinusDodgeChance;

        // HP remaining after first blow / damagePerAttackAttacker so numAttacksAttackerAfterFirstBlow may be 0
        int numAttacksAttackerAfterFirstBlow = Mathf.CeilToInt( Mathf.Max(0.0f, (defender.HP - attacker.FirstBlowDamage)) / damagePerAttackAttacker);
        int numAttacksDefender = Mathf.CeilToInt(attacker.HP / damagePerAttackDefender);

        if (numAttacksAttackerAfterFirstBlow < numAttacksDefender)// Attacker won since he starts
        {
            // Assume attacker is an ally
            int lostHP = (int)(numAttacksAttackerAfterFirstBlow * damagePerAttackDefender);
            int attackerLostHPScore = GetLostHPScore(attacker, lostHP);
            int defenderLostHPScore = GetLostHPScore(defender, defender.HP);

            attacker.IsOnStartPosAndNeverMoved = Constants.False;// Only the attacker if winning has this flag for an attack set
            attacker.HP -= (byte)lostHP;
            possibleMove.DestFieldAfter.Piece = attacker;

            //orign
            //int scoreChange = m_boardState.MovingSideScoreMul * (defender.AliveScore + defenderLostHPScore - attackerLostHPScore);

            int scoreChange = m_boardState.MovingSideScoreMul * (positiveScore + defender.AliveScore - attacker.AliveScore + defenderLostHPScore - attackerLostHPScore + attacker.HP);

            possibleMove.ScoreAfterMove = m_boardState.BoardScore + scoreChange;
            return true;
        }
        else
        {
            // Assume attacker is an ally
            int lostHP = (int)(attacker.FirstBlowDamage + (numAttacksDefender - 1) * damagePerAttackDefender);
            int attackerLostHPScore = GetLostHPScore(attacker, attacker.HP);
            int defenderLostHPScore = GetLostHPScore(defender, lostHP);

            defender.HP -= (byte)lostHP;
            possibleMove.DestFieldAfter.Piece = defender;

            // + defenderLostHPScore
            //int scoreChange = m_boardState.MovingSideScoreMul * (defender.AliveScore + defenderLostHPScore - attackerLostHPScore - attacker.AliveScore);

            int scoreChange = m_boardState.MovingSideScoreMul * (positiveScore + defender.AliveScore - attacker.AliveScore + defenderLostHPScore - attackerLostHPScore - defender.HP);

            possibleMove.ScoreAfterMove = m_boardState.BoardScore + scoreChange;
            return false;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int GetLostHPScore(AIBoardState.PieceData piece, int lostHP)
    {
        return Constants.HPLostPatternMultiplier[piece.MovementPattern] * piece.Attack * lostHP;
    }

}
