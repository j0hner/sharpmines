using System.CommandLine;
using System.Threading.Tasks;

internal class Program
{
    static int BoardHeight;
    static int BoardWidth;
    static int MineCount;
    static int FlagCount;
    static int Correct;

    static readonly Random rng = new();
    static byte[,] GlobalBoard = { };
    static bool IsRunning = true;
    static bool IsFirstMove = true;

    /*
        Byte data layout:
        
        0bMCF_cccc

        M = mine
        C = covered
        F = flagged
        c = count (0-8)
                               MCF  cccc */
    const byte mineMask    = 0b1000_0000;
    const byte coverMask   = 0b0100_0000;
    const byte uncoverMask = 0b1011_1111;
    const byte flagMask    = 0b0010_0000;
    const byte unflagMask  = 0b1101_1111;
    const byte countMask   = 0b0000_1111;

    const string darkCoveredBg    = "\x1b[48;5;28m";
    const string lightCoveredBg   = "\x1b[48;5;34m";
    const string darkUncoveredBg  = "\x1b[48;5;179m";
    const string lightUncoveredBg = "\x1b[48;5;221m";
    const string selectedBg       = "\x1b[48;5;250m";
    const string flagFg           = "\x1b[38;5;196m";
    const string mineFg           = "\x1b[38;5;52m";
    const string reset            = "\x1b[39;49m";

    static readonly Dictionary<int, string> CountColors = new()
    {
        {1, $"\x1b[38;5;26m1" },
        {2, $"\x1b[38;5;34m2" },
        {3, $"\x1b[38;5;124m3"},
        {4, $"\x1b[38;5;128m4"},
        {5, $"\x1b[38;5;166m5"},
        {6, $"\x1b[38;5;31m6" },
        {7, $"\x1b[38;5;234m7"},
        {8, $"\x1b[38;5;242m8"},
    };

    static async Task<int> Main(string[] args)
    {      
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
                if (value < 1)
                {
                    result.AddError("There must be at least one mine on the board");
                }

                if (result.GetValue(countOpt) > result.GetValue(heightOpt) * result.GetValue(widthOpt))
                {
                    result.AddError("There cannot be more mines than spaces.");
                }
            }
        );

        Command custom = new("custom", "Make a custom game")
        {
            widthOpt,
            heightOpt,
            countOpt
        };

        rootCommand.Subcommands.Add(custom);

        easy.SetAction(_ => Game(9, 9, 10));
        medium.SetAction(_ => Game(16, 16, 40));
        hard.SetAction(_ => Game(30, 16, 99));

        custom.SetAction((ParseResult r) => Game(r.GetValue(widthOpt), r.GetValue(heightOpt), r.GetValue(countOpt)));

        ParseResult result = rootCommand.Parse(args);
        return result.Invoke();
    }

    static void Game(int boardWidth, int boardHeight, int mineCount)
    {
        Console.Write("\x1b[?1049h");
        
        BoardWidth = boardWidth;
        BoardHeight = boardHeight;
        MineCount = mineCount;

        while (true)
        {
            Console.Clear();
            (int y, int x) selected = (0, 0);
            
            GlobalBoard = new byte[boardHeight, boardWidth];
            for(int y = 0; y < BoardHeight; y++)
                for(int x = 0; x < BoardWidth; x++)
                    GlobalBoard[y, x] |= coverMask;

            IsRunning = true;
            IsFirstMove = true;
            FlagCount = mineCount;
            Correct = 0;
            
            while (IsRunning)
            {
                RenderBoard(GlobalBoard, selected);

                ConsoleKey key = Console.ReadKey(true).Key;
                
                byte space = GlobalBoard[selected.y, selected.x];

                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.H:
                        if (selected.x == 0) break;

                        selected.x -= 1;

                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.L:
                        if (selected.x == boardWidth - 1) break;

                        selected.x += 1;

                        break;

                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        if (selected.y == 0) break;

                        selected.y -= 1;

                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        if (selected.y == boardHeight - 1) break;

                        selected.y += 1;

                        break;

                    case ConsoleKey.Enter:
                    case ConsoleKey.D:
                        if (IsFirstMove)
                        {
                            GlobalBoard = GenerateSafeBoard(BoardWidth, BoardHeight, MineCount, selected);
                            IsFirstMove = false;
                        };

                        if (IsFlagged(space)) break;

                        if (!IsCovered(space) && GetCount(space) != 0)
                        {
                            ChordDig(GlobalBoard, selected);
                            break;
                        }

                        FloodDig(GlobalBoard, selected);

                        break;

                    case ConsoleKey.M:
                    case ConsoleKey.F:
                        if (IsFirstMove)
                        {
                            GlobalBoard = GenerateUnsafeBoard(BoardWidth, BoardHeight, MineCount, selected);
                            IsFirstMove = false;
                        };

                        if (!IsCovered(space)) break;

                        if (IsFlagged(space)) Unflag(GlobalBoard, selected);
                        else Flag(GlobalBoard, selected);

                        break;

                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        IsRunning = false;
                        break;
                }

                if (Correct == MineCount) Win();
            }

            string answer;
            do
            {
                Console.Write("\e[2KPlay again? [y/n]: ");
                answer = Console.ReadLine() ?? "";
            } while (answer != "y" && answer != "n");

            if (answer == "n") break;
        }

        Console.Write("\x1b[?1049l");
    }

    static void FloodDig(byte[,] board, (int y, int x) startCoords, bool isSolver = false)
    {
        Stack<(int y, int x)> FloodStack = new();
        FloodStack.Push(startCoords);

        byte space = board[startCoords.y, startCoords.x];

        if (HasMine(space)) GameOver(startCoords);

        while (FloodStack.Count > 0)
        {
            (int y, int x) coords = FloodStack.Pop();
            space = board[coords.y, coords.x];

            Uncover(board, coords);

            if (!isSolver && IsFlagged(space)) Unflag(board, coords);

            if (GetCount(space) != 0) continue;

            for (int y = coords.y - 1; y <= coords.y + 1; y++)
            {
                for (int x = coords.x - 1; x <= coords.x + 1; x++)
                {
                    if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                    
                    if (!IsCovered(board[y, x])) continue;

                    FloodStack.Push((y, x));
                }
            }
        }
    }

    static void ChordDig(byte[,] board, (int y, int x) startCoords, bool isSolver = false)
    {
        // solver knows what it's doing
        if (!isSolver) {
            int flags = 0;
            for (int y = startCoords.y - 1; y <= startCoords.y + 1; y++)
                for (int x = startCoords.x - 1; x <= startCoords.x + 1; x++)
                {
                    if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                    if (IsFlagged(board[y, x])) flags ++;
                }
            
            if (flags != GetCount(board[startCoords.y, startCoords.x])) return;
        }

        for (int y = startCoords.y - 1; y <= startCoords.y + 1; y++)
        {
            for (int x = startCoords.x - 1; x <= startCoords.x + 1; x++)
            {
                if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                
                byte space = board[y, x];

                if (IsFlagged(space)) continue;

                FloodDig(board, (y, x), isSolver);

                if (HasMine(space)) 
                {
                    GameOver((y, x));
                    return;
                }
            }
        }
    }    
    
    static void Win()
    {
        IsRunning = false;

        ShowBoard(GlobalBoard);

        RenderBoard(GlobalBoard, (-1, -1), "\x1b[1m\x1b[38;5;76mYou win!\x1b[0m");
    }

    static void GameOver((int y, int x) selected)
    {
        IsRunning = false;

        ShowBoard(GlobalBoard);

        RenderBoard(GlobalBoard, selected, "\x1b[1m\x1b[38;5;196mGame Over!\x1b[0m");
    }
        
    static void ShowBoard(byte[,] board)
    {
        for (int y = 0; y < BoardHeight; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                Uncover(board, (y, x));
            }
        }
    }

    static void RenderBoard(byte[,] board, (int y, int x) selected, string message = "")
    {
        string str = "";

        string[] ui = [
            "[Eventually a timer]",
            "--------------------",
            $"\x1b[1m{flagFg}F\x1b[0m{reset} {FlagCount,3}",
            "",
            $"{message}",
            ""
        ];

        for (int y = 0; y < BoardHeight; y++)
        {
            str += "\x1b[1m";
            for (int x = 0; x < BoardWidth; x++)
            {
                byte space = board[y, x];
                bool isDark = (y % 2 + x) % 2 == 1;
                bool isSelected = y == selected.y && x == selected.x;
                bool isCovered = IsCovered(space);

                string bg = isDark ? darkUncoveredBg : lightUncoveredBg;
                if (isCovered)  bg = isDark ? darkCoveredBg : lightCoveredBg;
                if (isSelected) bg = selectedBg;

                str += bg;

                if (IsFlagged(space))
                {
                    string flagStr = $"{flagFg}F";

                    if (!isCovered && !HasMine(space))
                        flagStr = $"{flagFg}X";

                    str += flagStr;
                }
                else if (HasMine(space) && !isCovered)
                {
                    str += $"{mineFg}#";
                }
                else if (TryGetCount(space, out byte count))
                    str += CountColors[count];
                else
                    str += " ";

                str += reset;
            }
            
            string uiLine = "";
            if (y < ui.Length) uiLine = ui[y];

            str += $" \x1b[0m{uiLine}\n";
        }

        Console.SetCursorPosition(0, 0);
        Console.WriteLine(str);
        // Console.SetCursorPosition(Board.GetLength(1) + 1, ui.Length);
    }

    static byte[,] GenerateUnsafeBoard(int width, int height, int mineCount, (int y, int x) startCoords)
    {
        byte[,] board = new byte[height, width];

        for (int i = 0; i < mineCount; i++)
        {
            int x, y;
            do
            {
                x = rng.Next(width);
                y = rng.Next(height);
            }
            while (HasMine(board[y, x]) || Math.Abs(startCoords.y - y) + Math.Abs(startCoords.x - x) <= 2);

            board[y, x] = mineMask;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                board[y, x] |= coverMask;

                if (HasMine(board[y, x])) continue;

                byte count = 0;
                for (int j = y - 1; j <= y + 1; j++)
                {
                    for (int k = x - 1; k <= x + 1; k++)
                    {
                        if (j < 0 || k < 0 || j >= height || k >= width) continue;

                        if (HasMine(board[j, k])) count++;
                    }
                }

                board[y, x] |= count;
            }
        }

        return board;
    }

    static byte[,] GenerateSafeBoard(int width, int height, int mineCount, (int y, int x) startCoords)
    {
        byte[,] board = {}, solverBoard;

        for (int i = 0; i < 10; i++)
        {
            board = GenerateUnsafeBoard(width, height, mineCount, startCoords);
            solverBoard = (byte[,])board.Clone();

            Console.Write($"\rSolving guessfree board. {i}");
            
            if (IsSolvable(solverBoard, startCoords))
                break;
        }

        FlagCount = mineCount;
        Correct = 0;

        return board;
    }

    static bool IsCovered(byte space) => (space & coverMask) != 0;
    static bool HasMine(byte space) => (space & mineMask) != 0;
    static bool IsFlagged(byte space) => (space & flagMask) != 0;
    static byte GetCount(byte space) => (byte)(space & countMask);
    static bool TryGetCount(byte space, out byte count)
    {
        count = GetCount(space);

        return count > 0 && !IsCovered(space);
    }
    
    static void Uncover(byte[,] board, (int y, int x) coords) => board[coords.y, coords.x] &= uncoverMask;
    static void Flag(byte[,] board, (int y, int x) coords, bool isSolver = false)
    {
        if (FlagCount <= 0) return;


        board[coords.y, coords.x] |= flagMask;
        
        if (isSolver) return;

        if (HasMine(board[coords.y, coords.x])) Correct++;
        FlagCount--;
    }    
    static void Unflag(byte[,] board, (int y, int x) coords, bool isSolver = false)
    {
        byte space = board[coords.y, coords.x];

        board[coords.y, coords.x] &= unflagMask;

        if (isSolver) return;

        if (HasMine(space)) Correct--;
        FlagCount++;
    } 
    static IEnumerable<(int y, int x)> GetNeighbors(int y, int x)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int ny = y + dy, nx = x + dx;
                if (ny >= 0 && ny < BoardHeight && nx >= 0 && nx < BoardWidth)
                    yield return (ny, nx);
            }
        }
    }

    static bool IsSolvable(byte[,] board, (int y, int x) startCoords)
    {
        FloodDig(board, startCoords, isSolver: true);

        int maxSteps = 10000;

        for(int i = 0; i < maxSteps; i++)
        {
            if(!SolveStep(board)) return false;
            if (IsBoardCleared(board)) return true;
        }

        return IsBoardCleared(board);
    }

    static bool SolveStep(byte[,] board)
    {
        bool progress = false;

        for (int y = 0; y < BoardHeight; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                byte space = board[y, x];
                if (IsCovered(space) || GetCount(space) == 0) continue;

                RenderBoard(board, (y, x));

                List<(int y, int x)> neighbors = GetNeighbors(y, x).ToList();
                List<(int y, int x)> hidden = neighbors.Where(
                    n => IsCovered(board[n.y, n.x]) && 
                    !IsFlagged(board[n.y, n.x])
                ).ToList();
                
                if (hidden.Count == 0) continue;
                
                int flags = neighbors.Count(n => IsFlagged(board[n.y, n.x]));
                int effectiveCount = GetCount(space) - flags;

                // All remaining hidden neighbors are mines
                if (hidden.Count == effectiveCount)
                {
                    foreach ((int hy, int hx) in hidden)
                    {
                        Flag(board, (hy, hx), isSolver: true);
                        progress = true;
                    }
                }

                // All remaining hidden neighbors are safe -> Cascade open
                else if (effectiveCount == 0)
                {
                    ChordDig(board, (y, x), isSolver: true);
                    progress = true;
                }
            }
        }

        if (!progress)
        {
            progress = DeduceSubsets(board);
        }

        return progress;
    }

    static bool DeduceSubsets(byte[,] board)
    {
        bool progress = false;
        var equations = new List<(List<(int y, int x)> hidden, int eff)>();

        for (int y = 0; y < BoardHeight; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                byte space = board[y, x];
                if (IsCovered(space) || GetCount(space) == 0) continue;

                var neighbors = GetNeighbors(y, x).ToList();
                var hidden = neighbors.Where(n => IsCovered(board[n.y, n.x]) && !IsFlagged(board[n.y, n.x])).ToList();
                int flags = neighbors.Count(n => IsFlagged(board[n.y, n.x]));
                int eff = GetCount(space) - flags;

                if (hidden.Count > 0)
                    equations.Add((hidden, eff));
            }
        }

        foreach (var eq1 in equations)
        {
            foreach (var eq2 in equations)
            {
                if (ReferenceEquals(eq1.hidden, eq2.hidden)) continue;

                // Check if eq1 is a strict subset of eq2
                if (eq1.hidden.All(h => eq2.hidden.Contains(h)))
                {
                    var diff = eq2.hidden.Where(h => !eq1.hidden.Contains(h)).ToList();
                    int diffEff = eq2.eff - eq1.eff;

                    if (diff.Count > 0)
                    {
                        // Difference tiles are 100% safe
                        if (diffEff == 0)
                        {
                            foreach (var (dy, dx) in diff)
                            {
                                if (IsCovered(board[dy, dx]))
                                {
                                    FloodDig(board, (dy, dx));
                                    progress = true;
                                }
                            }
                        }
                        // Difference tiles are 100% mines
                        else if (diffEff == diff.Count)
                        {
                            foreach (var (dy, dx) in diff)
                            {
                                if (!IsFlagged(board[dy, dx]))
                                {
                                    Flag(board, (dy, dx));
                                    progress = true;
                                }
                            }
                        }
                    }
                }
            }
            if (progress) break;
        }

        return progress;
    }

    static bool IsBoardCleared(byte[,] board)
    {
        int safeTileCount = (BoardHeight * BoardWidth) - MineCount;
        int uncovered = 0;

        for (int y = 0; y < BoardHeight; y++)
            for (int x = 0; x < BoardWidth; x++)
                if (!IsCovered(board[y, x])) uncovered++;

        return uncovered == safeTileCount;
    }
}
