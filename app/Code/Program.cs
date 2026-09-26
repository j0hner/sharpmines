using System.CommandLine;
using sharpmines.Game;
using sharpmines.Terminal;

namespace sharpmines;

internal class Program
{
    static int SolverSteps = 1000;

    static async Task<int> Main(string[] args)
    {
        Option<int> solverStepOpt = new("max-steps", ["-s", "--max-steps"])
        {
            Description = "The maximum ammount of steps the solver can make, before giving up. Setting this to 0 will disable the solver.",
            DefaultValueFactory = _ => 1000
        };

        solverStepOpt.Validators.Add(
            (System.CommandLine.Parsing.OptionResult result) =>
            {
                int value = result.GetValue(solverStepOpt);
                if (value < 0) result.AddError("Max steps must be 0 or more");
            }
        );
        
        RootCommand rootCommand = new("Play a game of minesweeper, right in your terminal.");

        Command easy = new("easy", "Easy preset (9x9, 10 mines)");
        Command medium = new("medium", "Medium preset (16x16, 40 mines)");
        Command hard = new("hard", "Hard preset (30x16, 99 mines)");

        rootCommand.Subcommands.Add(easy);
        rootCommand.Subcommands.Add(medium);
        rootCommand.Subcommands.Add(hard);

        Option<int> widthOpt = new("width", aliases: ["-w", "--width"])
        {
            Description = "Width of the custom board",
            Required = true
        };

        widthOpt.Validators.Add(
            (System.CommandLine.Parsing.OptionResult result) =>
            {
                int value = result.GetValue(widthOpt);
                if (value < 1)
                {
                    result.AddError("Width must be at least 1.");
                }
            }
        );

        Option<int> heightOpt = new("height", aliases: ["-h", "--height"])
        {
            Description = "Height of the custom board",
            Required = true
        };

        heightOpt.Validators.Add(
            (System.CommandLine.Parsing.OptionResult result) =>
            {
                float value = result.GetValue(heightOpt);
                if (value < 1)
                {
                    result.AddError("Height must be at least 1.");
                }
            }
        );

        Option<int> countOpt = new("count", aliases: ["-c", "--count"])
        {
            Description = "The explicit ammount of mines on the board",
            Required = true

        };

        countOpt.Validators.Add(
            (System.CommandLine.Parsing.OptionResult result) =>
            {
                float value = result.GetValue(countOpt);
                if (value < 1) result.AddError("There must be at least one mine on the board");

                long totalSpaces = result.GetValue(heightOpt) * result.GetValue(widthOpt);

                if (value > totalSpaces) result.AddError("There cannot be more mines than spaces.");
            }
        );

        Command custom = new("custom", "Make a custom game")
        {
            widthOpt,
            heightOpt,
            countOpt
        };

        rootCommand.Subcommands.Add(custom);

        easy.SetAction(r => {
            SolverSteps = r.GetValue(solverStepOpt);
            Game(9, 9, 10);
        });

        medium.SetAction(r => {
            SolverSteps = r.GetValue(solverStepOpt);
            Game(16, 16, 40);
        });

        hard.SetAction(r => {
            SolverSteps = r.GetValue(solverStepOpt);
            Game(30, 16, 99);
        });

        custom.SetAction(r => {
            SolverSteps = r.GetValue(solverStepOpt);
            Game(r.GetValue(widthOpt), r.GetValue(heightOpt), r.GetValue(countOpt));
        });
        
        Command[] cmds = [easy, medium, hard, custom];
        
        foreach (Command command in cmds) command.Options.Add(solverStepOpt);
        
        ParseResult result = rootCommand.Parse(args);
        return result.Invoke();
    }

    static void Game(int boardWidth, int boardHeight, int mineCount)
    {
        TerminalManager.SetupTerminal();

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

                if (game.Board.IsCleared())
                {
                    game.Win();
                }
            }

            if (!TerminalManager.AskReplay()) break;
        }

        TerminalManager.CleanupTerminal();
    }

}