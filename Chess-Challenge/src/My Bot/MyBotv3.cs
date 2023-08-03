using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBotv3 : IChessBot
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

    private int GetPieceSquareBonus(PieceType type, bool isWhite, int file, int rank)
    {
        if (file > 3)
            file = 7 - file;
        if (isWhite)
            rank = 7 - rank;
        sbyte unpackedData = unchecked((sbyte)((PackedEvaluationTables[rank, file] >> 8 * ((int)type - 1)) & 0xFF));
        return isWhite ? unpackedData : -unpackedData;
    }

    public int eval(Board board, IEnumerable<PieceList> my_pieces, IEnumerable<PieceList> enemy_pieces)
    {
        var my_value = my_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();
        var enemy_value = enemy_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();

        var relevantPieceTypes = new List<PieceType> { PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Queen };
        int activityDiff = 0;

        int whiteBonus = 0;
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            foreach (Piece piece in pieceList)
            {
                int val = GetPieceSquareBonus(piece.PieceType, piece.IsWhite, piece.Square.File, piece.Square.Rank);
                whiteBonus += board.IsWhiteToMove ? val : -val;
            }
        }

        foreach (PieceType pieceType in relevantPieceTypes)
        {
            var myPieces = my_pieces.Where(c => c.TypeOfPieceInList == pieceType);
            var enemyPieces = enemy_pieces.Where(c => c.TypeOfPieceInList == pieceType);

            foreach (PieceList? pieceList in myPieces)
            {
                foreach (Piece piece in pieceList)
                {
                    activityDiff += BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, piece.Square, board, board.IsWhiteToMove)) * 10;
                }
            }
            foreach (PieceList? pieceList in enemyPieces)
            {
                foreach (Piece piece in pieceList)
                {
                    activityDiff -= BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, piece.Square, board, board.IsWhiteToMove)) * 10;
                }
            }
        }


        return my_value - enemy_value + activityDiff + whiteBonus;
    }

    public Move Think(Board board, Timer timer)
    {
        mDepth = 4;
        EvaluateBoardNegaMax(board, mDepth, -kMassiveNum, kMassiveNum, board.IsWhiteToMove ? 1 : -1);
        return mBestMove;
    }

    int EvaluateBoardNegaMax(Board board, int depth, int alpha, int beta, int color)
    {
        Move[] legalMoves;

        if (board.IsDraw())
            return 0;

        if (depth == 0 || (legalMoves = board.GetLegalMoves()).Length == 0)
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