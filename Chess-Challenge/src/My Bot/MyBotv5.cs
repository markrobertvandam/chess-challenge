using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBotv5 : IChessBot
{
    //                     .  P    K    B    R    Q    K
    int[] pieceValues = { 0, 100, 300, 310, 500, 900, 10000 };

    Move mBestMove;

    struct TTEntry
    {
        public ulong key;
        public Move move;
        public int depth, bound;
        public int score;
        public TTEntry(ulong _key, Move _move, int _depth, int _score, int _bound)
        {
            key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
        }
    }

    const int entries = (1 << 25);
    TTEntry[] tt = new TTEntry[entries];

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

    public List<Move> orderLegalMoves(Move[] legalMoves, Move bestMove)
    {
        //Console.WriteLine($"Previous best move: {bestMove}");
        List<Move> sortedMoves = legalMoves
            .Select(c => new Tuple<Move, int>(c, MVV_VLA[(int)c.CapturePieceType, (int)c.MovePieceType]))
            .OrderByDescending(t => t.Item2)
            .Select(c => c.Item1)
            .ToList();


        if (!bestMove.IsNull)
        {
            sortedMoves.Remove(bestMove);
            sortedMoves.Insert(0, bestMove);
        }
        return sortedMoves;
    }

    public int eval(Board board, IEnumerable<PieceList> white_pieces, IEnumerable<PieceList> black_pieces)
    {
        int activityDiff = 0;
        int whiteBonus = 0;

        foreach (PieceType pieceType in new List<PieceType> { PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Queen })
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


        return (white_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum() - black_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum() + activityDiff + whiteBonus) * (board.IsWhiteToMove ? 1 : -1);
    }

    public int Search(Board board, Timer timer, int alpha, int beta, int depth, int ply)
    {

        bool qsearch = depth <= 0;
        bool root = ply <= 0;
        int recordEval = -30000;

        // Check for repetition (this is much more important than material and 50 move rule draws)
        if (!root && board.IsRepeatedPosition())
            return 0;

        ulong key = board.ZobristKey;
        TTEntry entry = tt[key % entries];

        if (!root && entry.key == key && entry.depth >= depth && (
            entry.bound == 3 // exact score
            || entry.bound == 2 && entry.score >= beta // lower bound, fail high
            || entry.bound == 1 && entry.score <= alpha // upper bound, fail low
        )) return entry.score;

        int evaluation = eval(board, board.GetAllPieceLists().Where(c => c.IsWhitePieceList == true), board.GetAllPieceLists().Where(c => c.IsWhitePieceList == false));

        // Quiescence search is in the same function as negamax to save tokens
        if (qsearch)
        {
            recordEval = evaluation;
            if (recordEval >= beta)
                return recordEval;
            alpha = Math.Max(alpha, recordEval);
        }

        // Generate moves, only captures in qsearch
        List<Move> moves = orderLegalMoves(board.GetLegalMoves(qsearch), entry.key == key ? entry.move : Move.NullMove);

        Move bestIterMove = Move.NullMove;
        int InitialAlpha = alpha;

        // TREE SEARCH
        for (int i = 0; i < moves.Count; i++)
        {
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 40)
                return 30000;

            Move move = moves[i];
            board.MakeMove(move);
            int eval = -Search(board, timer, -beta, -alpha, depth - 1, ply + 1);
            board.UndoMove(move);

            // New best move
            if (eval > recordEval)
            {
                recordEval = eval;
                bestIterMove = move;
                if (ply == 0) mBestMove = move;

                // Improve alpha
                alpha = Math.Max(alpha, eval);

                // Fail-high
                if (alpha >= beta)
                    break;

            }
        }
        // TREE SEARCH

        // (Check/Stale)mate
        if (!qsearch && moves.Count == 0) return board.IsInCheck() ? -30000 + ply : 0;

        // Did we fail high/low or get an exact score?
        int bound = recordEval >= beta ? 2 : recordEval > InitialAlpha ? 3 : 1;

        // Push to TT
        tt[key % entries] = new TTEntry(key, bestIterMove, depth, recordEval, bound);

        return recordEval;
    }

    public Move Think(Board board, Timer timer)
    {
        mBestMove = Move.NullMove;
        // Console.WriteLine($"Time for move: {timer.MillisecondsRemaining / 30}");
        for (int depth = 1; depth <= 50; depth++)
        {
            //if (depth > 6)
            //{
            //    Console.WriteLine($"Evaluating at depth {depth}");
            //}
            // Console.WriteLine($"Evaluating at depth: {depth}");

            var eval = Search(board, timer, -30000, 30000, depth, 0);
            // Console.WriteLine($"Evaluated at depth {depth}, best move so far: {mBestMove}, eval: {eval}, time spent: {timer.MillisecondsElapsedThisTurn}");

            // Out of time
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 40) break;
        }
        // No time to select a move
        return mBestMove.IsNull ? board.GetLegalMoves()[0] : mBestMove;

    }
}