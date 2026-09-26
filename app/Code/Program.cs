using System.CommandLine;
using sharpmines.Game;
using sharpmines.Terminal;

namespace sharpmines;

internal class Sharpmines
{
    public static int SolverSteps = 1000;

    static async Task<int> Main(string[] args)
    {
        TerminalManager.SetupTerminal();
        
        return CliHandler.MakeRootCommand().Parse(args).Invoke();
    }

    public static void Game(int boardWidth, int boardHeight, int mineCount)
    {
        

        while (true)
        {
            Console.Clear();
            MinesweeperGame game = new(boardWidth, boardHeight, mineCount, SolverSteps);

            while (game.IsRunning)
            {
                TerminalManager.Render(game);

                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.KeyChar == '?') TerminalManager.HelpMenu();

                switch (key.Key)
                {
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.H:
                        game.Move((0, -1));
                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.L:
                        game.Move((0, 1));
                        break;

                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        game.Move((-1, 0));
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        game.Move((1, 0));
                        break;

                    case ConsoleKey.Spacebar:
                    case ConsoleKey.D:
                        game.Dig();
                        break;

                    case ConsoleKey.M:
                    case ConsoleKey.F:
                        game.ToggleFlag();
                        break;

                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        game.IsRunning = false;
                        break;
                }

                if (game.Board.IsCleared() && game.Correct == game.Board.MineCount) game.Win();
            }

            if (!TerminalManager.AskReplay()) break;
        }

        TerminalManager.CleanupTerminal();
    }

}