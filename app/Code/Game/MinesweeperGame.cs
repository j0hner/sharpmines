using sharpmines.Terminal;

namespace sharpmines.Game;

public class MinesweeperGame(int boardWidth, int boardHeight, int mineCount, int solverSteps)
{
    public int FlagCount { get; set; } = mineCount;
    public int Correct { get; set; } = 0;

    public bool IsRunning { get; set; } = true;
    public bool IsFirstMove { get; set; } = true;

    public (int y, int x) Selected = (0,0);

    public Board Board {get; set;} = new Board(boardWidth, boardHeight, mineCount);

    public void Move((int dy, int dx) move)
    {
        Selected.y += move.dy;
        Selected.x += move.dx;

        Selected.y = Math.Clamp(Selected.y, 0, Board.Height - 1);
        Selected.x = Math.Clamp(Selected.x, 0, Board.Width - 1);
    }

    public void Dig()
    {
        if (Board.IsFlagged(Selected)) return;
        
        if (IsFirstMove)
        {
            Board.TryGenerateGuessfree(Selected, solverSteps);
            IsFirstMove = false;
        }
        
        if (Board.IsCovered(Selected))
        {
            if(!Board.FloodUncover(Selected)) GameOver();
        }
        else
        {
            int flags = Board.GetNeighborCoords(Selected).Count(Board.IsFlagged);
            if(Board.GetCount(Selected) != flags) return;

            if(!Board.ChordUncover(Selected)) GameOver();
        }
    }

    public void ToggleFlag()
    {
        if (!Board.IsCovered(Selected)) return;
        
        if (IsFirstMove)
        {
            Board.GenerateRandom(Selected);
            IsFirstMove = false;
        }

        if (Board.IsFlagged(Selected))
        {
            Board.Unflag(Selected);

            if (Board.HasMine(Selected)) Correct--;
            FlagCount++;
        }
        else
        {
            Board.Flag(Selected);

            if (Board.HasMine(Selected)) Correct++;
            FlagCount--;
        }
    }

    public void GameOver()
    {
        IsRunning = false;
        Board.Reveal();
        TerminalManager.Render(this, "You Lose!");
    }
    
    public void Win()
    {
        IsRunning = false;
        Selected = (-1, -1);
        Board.Reveal();
        TerminalManager.Render(this, "You Win!");
    }
}