namespace sharpmines.Game;

public class GameState(int boardWidth, int boardHeight, int mineCount)
{
    public int FlagCount { get; set; } = 0;
    public int Correct { get; set; } = 0;

    public bool IsRunning { get; set; } = true;
    public bool IsFirstMove { get; set; } = true;

    public (int y, int x) Selected = (0,0);

    public Board Board {get; set;} = new Board(boardWidth, boardHeight, mineCount);
}