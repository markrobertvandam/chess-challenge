using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBotv1 : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };


    public int eval(IEnumerable<PieceList> my_pieces, IEnumerable<PieceList> enemy_pieces)
    {
        var my_value = my_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();
        var enemy_value = enemy_pieces.Select(c => c.Count * pieceValues[(int)c.TypeOfPieceInList]).Sum();

        return my_value - enemy_value;
    }

    public int eval_after_move(Board board, Move move)
    {
        board.MakeMove(move);
        int evaluation = eval(board.GetAllPieceLists().Where(c => c.IsWhitePieceList == board.IsWhiteToMove), board.GetAllPieceLists().Where(c => c.IsWhitePieceList != board.IsWhiteToMove));

        board.UndoMove(move);

        return evaluation;
    }

    public Move ThinkGen(Board board, Timer timer, bool isBot)
    {
        Move[] allMoves = board.GetLegalMoves();
        List<Tuple<Move, int>> candidateMoves = new List<Tuple<Move, int>>();

        // Pick a random move to play if nothing better is found
        Random rng = new();

        Move moveToPlay = allMoves[rng.Next(allMoves.Length)];

        int highestValueCapture = 0;

        foreach (Move move in allMoves)
        {
            // Always play checkmate in one
            if (MoveIsCheckmate(board, move))
            {
                return move;
            }

            if (isBot)
            {
                var initial_eval = eval(board.GetAllPieceLists().Where(c => c.IsWhitePieceList == board.IsWhiteToMove), board.GetAllPieceLists().Where(c => c.IsWhitePieceList != board.IsWhiteToMove));

                // try move 
                board.MakeMove(move);

                int updated_eval = 0;

                if (!board.IsInStalemate())
                {
                    // See what move enemy wants to make
                    Move enemy_move = ThinkGen(board, timer, false);
                    updated_eval = eval_after_move(board, enemy_move);
                }

                candidateMoves.Add(new(move, updated_eval));
                board.UndoMove(move);
            }

            else
            {
                // Find highest value capture
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

                if (capturedPieceValue > highestValueCapture)
                {
                    moveToPlay = move;
                    highestValueCapture = capturedPieceValue;
                }
            }
        }

        if (isBot)
        {
            var sorted_CandidateMoves = candidateMoves.OrderByDescending(t => t.Item2).ToList();
            moveToPlay = sorted_CandidateMoves[0].Item1;
        }

        return moveToPlay;
    }

    public Move Think(Board board, Timer timer)
    {
        return ThinkGen(board, timer, true);
    }

    // Test if this move gives checkmate
    bool MoveIsCheckmate(Board board, Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
}