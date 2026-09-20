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
    static byte[,] Board = { };
    static bool IsRunning = true;

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
            (int, int) selected = (0, 0);
            Board = GenerateBoard(boardWidth, boardHeight, mineCount);
            IsRunning = true;
            FlagCount = mineCount;
            Correct = 0;
            
            while (IsRunning)
            {
                RenderBoard(selected);

                ConsoleKey key = Console.ReadKey(true).Key;

                byte space = Board[selected.Item1, selected.Item2];

                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.H:
                        if (selected.Item2 == 0) break;

                        selected.Item2 -= 1;

                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.L:
                        if (selected.Item2 == boardWidth - 1) break;

                        selected.Item2 += 1;

                        break;

                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        if (selected.Item1 == 0) break;

                        selected.Item1 -= 1;

                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        if (selected.Item1 == boardHeight - 1) break;

                        selected.Item1 += 1;

                        break;

                    case ConsoleKey.Enter:
                    case ConsoleKey.D:
                        if (IsFlagged(space)) break;

                        if (!IsCovered(space) && GetCount(space) != 0)
                        {
                            ChordDig(selected);
                            break;
                        }

                        FloodDig(selected);

                        break;

                    case ConsoleKey.M:
                    case ConsoleKey.F:
                        if (!IsCovered(space)) break;

                        if (IsFlagged(space)) Unflag(selected);
                        else Flag(selected);

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
                Console.Write("Play again? [y/n]: ");
                answer = Console.ReadLine() ?? "";
            } while (answer != "y" && answer != "n");

            if (answer == "n") break;
        }

        Console.Write("\x1b[?1049l");
    }

    static void FloodDig((int, int) startCoords)
    {
        Stack<(int, int)> FloodStack = new();
        FloodStack.Push(startCoords);

        byte space = Board[startCoords.Item1, startCoords.Item2];

        if (HasMine(space)) GameOver(startCoords);

        while (FloodStack.Count > 0)
        {
            (int, int) coords = FloodStack.Pop();
            space = Board[coords.Item1, coords.Item2];

            Uncover(coords);

            if (IsFlagged(space)) Unflag(coords);

            if (GetCount(space) != 0) continue;

            for (int y = coords.Item1 - 1; y <= coords.Item1 + 1; y++)
            {
                for (int x = coords.Item2 - 1; x <= coords.Item2 + 1; x++)
                {
                    if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                    
                    if (!IsCovered(Board[y, x])) continue;

                    FloodStack.Push((y, x));
                }
            }
        }
    }

    static void ChordDig((int, int) startCoords)
    {
        int flags = 0;
        for (int y = startCoords.Item1 - 1; y <= startCoords.Item1 + 1; y++)
            for (int x = startCoords.Item2 - 1; x <= startCoords.Item2 + 1; x++)
            {
                if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                if (IsFlagged(Board[y, x])) flags ++;
            }
        
        if (flags != GetCount(Board[startCoords.Item1, startCoords.Item2])) return;
        
        for (int y = startCoords.Item1 - 1; y <= startCoords.Item1 + 1; y++)
        {
            for (int x = startCoords.Item2 - 1; x <= startCoords.Item2 + 1; x++)
            {
                if (y < 0 || x < 0 || y >= BoardHeight || x >= BoardWidth) continue;
                
                byte space = Board[y, x];

                if (IsFlagged(space)) continue;

                FloodDig((y, x));

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

        ShowBoard();

        RenderBoard((-1, -1));

        Console.WriteLine("\x1b[1m\x1b[38;5;76mYou win!\x1b[0m\n");
    }

    static void GameOver((int, int) selected)
    {
        IsRunning = false;

        ShowBoard();

        RenderBoard(selected);
        Console.WriteLine("\x1b[1m\x1b[38;5;196mGame Over!\x1b[0m\n");
    }
        
    static void ShowBoard()
    {
        for (int y = 0; y < BoardHeight; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                Uncover((y, x));
            }
        }
    }

    static void RenderBoard((int, int) selected)
    {
        string str = "\x1b[1m";
        for (int y = 0; y < BoardHeight; y++)
        {
            
            for (int x = 0; x < BoardWidth; x++)
            {
                byte space = Board[y, x];
                bool isDark = (y % 2 + x) % 2 == 1;
                bool isSelected = y == selected.Item1 && x == selected.Item2;
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
            str += "\n";
        }

        str += $"\x1b[1m\x1b[38;5;196mF\x1b[0m {FlagCount,3}";

        Console.SetCursorPosition(0, 0);
        Console.WriteLine(str);
    }

    static byte[,] GenerateBoard(int width, int height, int mineCount)
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
            while (HasMine(board[y, x]));

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

    static bool IsCovered(byte space) => (space & coverMask) != 0;
    static bool HasMine(byte space) => (space & mineMask) != 0;
    static bool IsFlagged(byte space) => (space & flagMask) != 0;
    static byte GetCount(byte space) => (byte)(space & countMask);
    static bool TryGetCount(byte space, out byte count)
    {
        count = GetCount(space);

        return count > 0 && !IsCovered(space);
    }

    static void Uncover((int, int) coords) => Board[coords.Item1, coords.Item2] &= uncoverMask;
    static void Flag((int, int) coords)
    {
        if (FlagCount <= 0) return;

        if (HasMine(Board[coords.Item1, coords.Item2])) Correct++;

        Board[coords.Item1, coords.Item2] |= flagMask;
        FlagCount--;
    }
    
    static void Unflag((int, int) coords)
    {
        byte space = Board[coords.Item1, coords.Item2];

        if (HasMine(space)) Correct--;
        
        Board[coords.Item1, coords.Item2] &= unflagMask;

        FlagCount++;
    } 
}
