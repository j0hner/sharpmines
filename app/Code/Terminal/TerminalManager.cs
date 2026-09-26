using sharpmines.Game;

namespace sharpmines.Terminal;

public static class TerminalManager
{
    const string darkCoveredBg = "\x1b[48;5;28m";
    const string lightCoveredBg = "\x1b[48;5;34m";
    const string darkUncoveredBg = "\x1b[48;5;179m";
    const string lightUncoveredBg = "\x1b[48;5;221m";
    const string selectedBg = "\x1b[48;5;250m";
    const string flagFg = "\x1b[38;5;196m";
    const string mineFg = "\x1b[38;5;52m";
    const string reset = "\x1b[39;49m";

    private static readonly Dictionary<int, string> CountColors = new()
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

    static readonly string[] Help = [
        "                      \e[1;4;32mKey actions\e[22;24;39m\n\n         \e[1;4;34mMove around\e[22;24;39m  \e[90m╎\e[39m   \e[1;4;34mDig\e[22;24;39m   \e[90m╎\e[39m  \e[1;4;34mPlace flags\e[22;24;39m\n                      \e[90m╎\e[39m         \e[90m╎\e[39m\n              k       \e[90m╎\e[39m         \e[90m╎\e[39m\n            h j l     \e[90m╎\e[39m    D    \e[90m╎\e[39m       F\n                      \e[90m╎\e[39m         \e[90m╎\e[39m\n              ↑       \e[90m╎\e[39m         \e[90m╎\e[39m\n            ← ↓ →     \e[90m╎\e[39m  Space  \e[90m╎\e[39m       M\n \e[90m⏎ Next               ╎\e[39m         \e[90m╎\e[39m",
        "                         \e[1;4;32mRules\e[22;24;39m\n\n   The board contains safe spots and \e[1mmines\e[22m. The goal\n     is to correctly \e[1mplace flags\e[22m on all the mines.\n                           -\nTo help you deduce where the mines are, some tiles have\n   a \e[1mnumber in them\e[22m.  This indicates how many of the\n             tile's 8 neighbors are mines.                   \n                           -\n \e[90m⏎ End\e[39m               Made by: \e[1;36mj0hner\e[22;24;39m"
    ];

    public static void Render(MinesweeperGame state, string message = "")
    {
        string str = "";

        string[] ui = [
            "[Eventually a timer]",
            "--------------------",
            $"\x1b[1m{flagFg}F\x1b[0m{reset} {state.FlagCount,3}",
            "",
            message
        ];

        for (int y = 0; y < state.Board.Height; y++)
        {
            str += "\x1b[1m";
            for (int x = 0; x < state.Board.Width; x++)
            {
                (int, int) coords = (y, x);
                
                bool isDark = (y % 2 + x) % 2 == 1;
                bool isSelected = y == state.Selected.y && x == state.Selected.x;
                bool isCovered = state.Board.IsCovered(coords);

                string bg = isDark ? darkUncoveredBg : lightUncoveredBg;
                if (isCovered) bg = isDark ? darkCoveredBg : lightCoveredBg;
                if (isSelected) bg = selectedBg;

                str += bg;

                if (state.Board.IsFlagged(coords))
                {
                    string flagStr = $"{flagFg}F";

                    if (!isCovered && !state.Board.HasMine(coords))
                        flagStr = $"{flagFg}X";

                    str += flagStr;
                }
                else if (state.Board.HasMine(coords) && !isCovered)
                {
                    str += $"{mineFg}#";
                }
                else if (state.Board.TryGetCount(coords, out byte count))
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
    }

    public static void HelpMenu()
    {
        foreach (string page in Help)
        {
            Console.Clear();
            Console.Write(page);

            do {} while (Console.ReadKey(true).Key != ConsoleKey.Enter);
        }

        Console.Clear();
    }

    public static void SetupTerminal()
    {
        Console.Write("\x1b[?1049h");
        Console.CursorVisible = false;
    }

    public static void CleanupTerminal()
    {
        Console.Write("\x1b[?1049l");
        Console.CursorVisible = true;
    }

    public static bool AskReplay()
    {
        Console.CursorVisible = true;
            
        ConsoleKey answer;
        do
        {
            Console.Write("\r\e[2KPlay again? [y/n]: ");
            answer = Console.ReadKey().Key;
        } while (answer != ConsoleKey.N && answer != ConsoleKey.Y);

        Console.CursorVisible = false;
        
        return answer == ConsoleKey.Y;
    }
}