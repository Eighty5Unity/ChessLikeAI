using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using System.Runtime.CompilerServices;

[BurstCompile(CompileSynchronously = true)]

public class AIBoardState
{
    public struct FieldData
    {
        public byte HasPiece;
        public byte FieldIdx;
        public sbyte PawnLongJumpOverDoneDepth;// At what depth was this field available for beating in passing
        public byte FreeByte2;
        public PieceData Piece;
    }

    public struct PieceData
    {
        public byte Side;
        public byte MovementPattern;
        public byte AttackPattern;
        public byte IsOnStartPosAndNeverMoved;// Allignment makes it faster

        public byte Attack;
        public byte FirstBlowDamage;
        public byte AliveScore;
        public byte HP;

        public float OneMinusDodgeChance;
    }

    public int BoardScore;
    public byte MovingSide;// Byte for compatibility
    public int MovingSideScoreMul; // 1 for the current side (max), -1 for the opposite side (min)

    public FieldData[] Board;// Arrray of 64 fields, only alive pieces are in it

    public void Create()
    {
        Board = new FieldData[Constants.BoardSize * Constants.BoardSize];
        FieldData emptyField = new FieldData();
        emptyField.HasPiece = Constants.False;
        emptyField.PawnLongJumpOverDoneDepth = -100;
        for (byte i = 0; i < Board.Length; ++i)
        {
            emptyField.FieldIdx = i;
            Board[i] = emptyField;
        }
    }


    public AIBoardState Clone()
    {
        AIBoardState retVal = (AIBoardState)this.MemberwiseClone();
        retVal.Board = (FieldData[])this.Board.Clone();
        return retVal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FieldData GetField(int x, int z)
    {
        return Board[GetFieldIdx(x, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetFieldIdx(int x, int z)
    {
        return (x << 3) + z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetX(int fieldIndex)
    {
        return fieldIndex >> 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetZ(int fieldIndex)
    {
        return (fieldIndex & 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnBoard(int x, int z)
    {
        bool isOutOfBoard = (x < 0) || (x >= Constants.BoardSize) || (z < 0) || (z >= Constants.BoardSize);
        return !isOutOfBoard;
    }
}
