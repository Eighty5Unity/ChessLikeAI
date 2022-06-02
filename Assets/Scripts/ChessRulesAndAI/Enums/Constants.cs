using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Constants
{
    public const int BoardSize = 8;
    public const int MaxPiecesPerSide = 16;

    public const byte ConflictSideWhite = (byte)ConflictSide.White;
    public const byte ConflictSideBlack = (byte)ConflictSide.Black;

    public const byte MovementPatternPawn = (byte)MovementPattern.Pawn;
    public const byte MovementPatternQueen = (byte)MovementPattern.Queen;
    public const byte MovementPatternBishop = (byte)MovementPattern.Bishop;
    public const byte MovementPatternKing = (byte)MovementPattern.King;
    public const byte MovementPatternKnight = (byte)MovementPattern.Knight;
    public const byte MovementPatternRook = (byte)MovementPattern.Rook;

    public const byte True = 1;
    public const byte False = 0;

    // Using inidices from piece enum
    // Pawn, Rook, Bishop, King (is alwyas max), Knight, Queen
    public static readonly int[] AliveScorePatternMultiplier = { 2, 8, 7, 20, 7, 9 };

    // Using inidices from piece enum
    // Pawn, Rook, Bishop, King, Knight, Queen
    public static readonly int[] HPLostPatternMultiplier = { 2, 5, 4, 15, 4, 6 };

    public static readonly string[] BoardLetters = { "A", "B", "C", "D", "E", "F", "G", "H" };
    public static readonly string[] BoardNumbers = { "1", "2", "3", "4", "5", "6", "7", "8" };

    /*
     * Attack:
     *  AliveScorePatternMultiplier = { 2, 8, 7, 20, 7, 9 }; 
     *  
     *  HPLostPatternMultiplier = { 1, 3, 2, 5, 2, 4 };
     */
}
