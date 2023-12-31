﻿using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBotv4 : IChessBot
{  

    //                     .  P    K    B    R    Q    K
    int[] pieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
    int kMassiveNum = 99999999;

    int mDepth;
    Move mBestMove;

    private readonly ulong[,] PackedEvaluationTables = {
        { 58233348458073600, 61037146059233280, 63851895826342400, 66655671952007680 },
        { 63862891026503730, 66665589183147058, 69480338950193202, 226499563094066 },
        { 63862895153701386, 69480338782421002, 5867015520979476,  8670770172137246 },
        { 63862916628537861, 69480338782749957, 8681765288087306,  11485519939245081 },
        { 63872833708024320, 69491333898698752, 8692760404692736,  11496515055522836 },
        { 63884885386256901, 69502350490469883, 5889005753862902,  8703755520970496 },
        { 63636395758376965, 63635334969551882, 21474836490,       1516 },
        { 58006849062751744, 63647386663573504, 63625396431020544, 63614422789579264 }
    };

    // MVV_VLA[victim][attacker]
    public readonly int[,] MVV_VLA = {
        {0, 0, 0, 0, 0, 0, 0},       // victim None, attacker P, K, B, R, Q, K
        {0, 15, 14, 13, 12, 11, 10}, // victim None, P, attacker P, K, B, R, Q, K
        {0, 25, 24, 23, 22, 21, 20}, // victim None, N, attacker P, K, B, R, Q, K
        {0, 35, 34, 33, 32, 31, 30}, // victim None, B, attacker P, K, B, R, Q, K
        {0, 40, 41, 42, 43, 44, 45}, // victim None, R, attacker P, K, B, R, Q, K
        {0, 55, 54, 53, 52, 51, 50}, // victim None, Q, attacker P, K, B, R, Q, K
    };

    private int GetPieceSquareBonus(PieceType type, bool isWhite, int file, int rank)
    {
        if (file > 3)
            file = 7 - file;
        if (isWhite)
            rank = 7 - rank;
        sbyte unpackedData = unchecked((sbyte)((PackedEvaluationTables[rank, file] >> 8 * ((int)type - 1)) & 0xFF));
        return isWhite ? unpackedData : -unpackedData;
    }

    public int eval(Board board, IEnumerable<PieceList> white_pieces, IEnumerable<PieceList> black_pieces)
    {
        var my_value = white_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();
        var enemy_value = black_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();

        var relevantPieceTypes = new List<PieceType> { PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Queen };
        int activityDiff = 0;

        int whiteBonus = 0;

        foreach (PieceType pieceType in relevantPieceTypes)
        {
            var whitePieceLists = white_pieces.Where(c => c.TypeOfPieceInList == pieceType);
            var blackPieceLists = black_pieces.Where(c => c.TypeOfPieceInList == pieceType);

            foreach (PieceList? whitePieceList in whitePieceLists)
            {
                foreach (Piece whitePiece in whitePieceList)
                {
                    int val = GetPieceSquareBonus(whitePiece.PieceType, whitePiece.IsWhite, whitePiece.Square.File, whitePiece.Square.Rank);
                    whiteBonus += val;
                    activityDiff += BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, whitePiece.Square, board, board.IsWhiteToMove)) * 5;
                }
            }
            foreach (PieceList? blackPieceList in blackPieceLists)
            {
                foreach (Piece blackPiece in blackPieceList)
                {
                    int val = GetPieceSquareBonus(blackPiece.PieceType, blackPiece.IsWhite, blackPiece.Square.File, blackPiece.Square.Rank);
                    whiteBonus -= val;
                    activityDiff -= BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, blackPiece.Square, board, board.IsWhiteToMove)) * 5;
                }
            }
        }


        return my_value - enemy_value + activityDiff + whiteBonus;
    }

    public List<Move> orderLegalMoves(Move[] legalMoves)
    {
        List<Move> sortedMoves = legalMoves
            .Select(c => new Tuple<Move, int>(c, MVV_VLA[(int)c.CapturePieceType, (int)c.MovePieceType]))
            .OrderByDescending(t => t.Item2)
            .Select(c => c.Item1)
            .ToList();

        return sortedMoves;
    }

    public Move Think(Board board, Timer timer)
    {
        mDepth = 5;
        EvaluateBoardNegaMax(board, mDepth, -kMassiveNum, kMassiveNum, board.IsWhiteToMove ? 1 : -1);
        return mBestMove;
    }

    int EvaluateBoardNegaMax(Board board, int depth, int alpha, int beta, int color)
    {
        List<Move> legalMoves;

        if (board.IsDraw())
            return 0;

        if (depth == 0 || (legalMoves = orderLegalMoves(board.GetLegalMoves())).Count == 0)
        {
            if (board.IsInCheckmate())
                return -999999 + mDepth - depth;

            int evaluation = eval(board, board.GetAllPieceLists().Where(c => c.IsWhitePieceList == true), board.GetAllPieceLists().Where(c => c.IsWhitePieceList == false));

            return color * evaluation;
        }

        // TREE SEARCH
        int recordEval = int.MinValue;
        foreach (Move move in legalMoves)
        {
            board.MakeMove(move);
            int evaluation = -EvaluateBoardNegaMax(board, depth - 1, -beta, -alpha, -color);
            board.UndoMove(move);

            if (recordEval < evaluation)
            {
                recordEval = evaluation;
                if (depth == mDepth)
                    mBestMove = move;
            }
            alpha = Math.Max(alpha, recordEval);
            if (alpha >= beta) break;
        }
        // TREE SEARCH

        return recordEval;
    }
}