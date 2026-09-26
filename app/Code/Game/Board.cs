using System.Diagnostics;
using sharpmines.Solver;

namespace sharpmines.Game;

public class Board
{
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

    private Random rng = new();
    private int Uncovered = 0;

    public int Height { get; }
    public int Width { get; }
    public int MineCount { get; }

    public bool IsPlayable { get; private set; } = false;

    private readonly byte[,] Data;
    public byte this[(int y, int x) coords]
    {
        get => Data[coords.y, coords.x];
        private set => Data[coords.y, coords.x] = value;
    }

    public Board(int boardWidth, int boardHeight, int mineCount)
    {
        Height = boardHeight;
        Width = boardWidth;
        MineCount = mineCount;

        Data = new byte[boardHeight, boardWidth];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Data[y, x] |= coverMask;
            }
        }
    }

    public Board(Board other)
    {
        Height = other.Height;
        Width = other.Width;
        MineCount = other.MineCount;

        Data = (byte[,])other.Data.Clone();
    }

    public bool IsCovered((int y, int x) coords) => (this[coords] & coverMask) != 0;
    public bool HasMine((int y, int x) coords) => (this[coords] & mineMask) != 0;
    public bool IsFlagged((int y, int x) coords) => (this[coords] & flagMask) != 0;
    public byte GetCount((int y, int x) coords) => (byte)(this[coords] & countMask);
    public bool TryGetCount((int y, int x) coords, out byte count)
    {
        count = GetCount(coords);

        return count > 0 && !IsCovered(coords);
    }
    public bool IsCleared() => Uncovered >= (Width * Height) - MineCount;

    public IEnumerable<(int y, int x)> GetNeighborCoords((int y, int x) coords)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int ny = coords.y + dy, nx = coords.x + dx;
                if (ny >= 0 && ny < Height && nx >= 0 && nx < Width)
                    yield return (ny, nx);
            }
        }
    }

    public void Uncover((int y, int x) coords, bool count = true)  {
        this[coords] &= uncoverMask;
        if (count) Uncovered++;
    }
    public void Flag((int y, int x) coords) => this[coords] |= flagMask;
    public void Unflag((int y, int x) coords) => this[coords] &= unflagMask;
    public void Reveal()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                (int, int) coords = (y, x);
                if (HasMine(coords) && IsFlagged(coords)) continue;
                
                Uncover(coords, count: false);
            }
        }
    }

    public bool FloodUncover((int y, int x) startCoords)
    {
        if (!IsCovered(startCoords))
            return true;

        Uncover(startCoords);

        if (GetCount(startCoords) != 0)
            return true;

        if (HasMine(startCoords))
            return false;
 
        Queue<(int y, int x)> floodQueue = new();
        floodQueue.Enqueue(startCoords);

        while (floodQueue.Count > 0)
        {
            foreach ((int y, int x) coords in GetNeighborCoords(floodQueue.Dequeue()))
            {
                if (!IsCovered(coords)) continue;

                Uncover(coords);
                byte count = GetCount(coords);

                if (count != 0) continue; // stop flooding
                else floodQueue.Enqueue(coords);
            }
        }

        return true;
    }

    public HashSet<(int y, int x)> FloodUncoverClues((int y, int x) startCoords)
    {
        HashSet<(int y, int x)> activeClues = [];

        if (!IsCovered(startCoords))
            return activeClues;

        Uncover(startCoords);

        if (GetCount(startCoords) != 0)
        {
            activeClues.Add(startCoords);
            return activeClues;
        }

        Queue<(int y, int x)> floodQueue = new();
        floodQueue.Enqueue(startCoords);

        while (floodQueue.Count > 0)
        {
            foreach ((int y, int x) coords in GetNeighborCoords(floodQueue.Dequeue()))
            {
                if (!IsCovered(coords)) continue;

                Uncover(coords);
                byte count = GetCount(coords);

                if (count > 0) activeClues.Add(coords); // stop flooding
                else floodQueue.Enqueue(coords);
            }
        }

        return activeClues;
    }

    public bool ChordUncover((int y, int x) startCoords)
    {
        IEnumerable<(int y, int x)> neighbors = GetNeighborCoords(startCoords);

        foreach ((int y, int x) coords in neighbors)
        {
            if (IsFlagged(coords)) continue;

            if (HasMine(coords)) return false;
            
            // guaranteed safe
            FloodUncover(coords);
        }

        return true;
    }

    public void GenerateRandom((int y, int x) startCoords)
    {
        for (int i = 0; i < MineCount; i++)
        {
            int x, y;
            do
            {
                x = rng.Next(Width);
                y = rng.Next(Height);
            }
            while (HasMine((y, x)) || Math.Abs(startCoords.y - y) + Math.Abs(startCoords.x - x) <= 2);

            this[(y, x)] = mineMask;
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                (int y, int x) coords = (y, x);
                
                this[coords] |= coverMask;

                if (HasMine(coords)) continue;

                byte count = 0;
                foreach ((int j, int k) neighbor in GetNeighborCoords((y, x)))
                    if(HasMine(neighbor)) count++;

                this[coords] |= count;
            }
        }
    }

    public bool TryGenerateGuessfree((int y, int x) startCoords, int SolverSteps)
    {
        if (SolverSteps == 0) {
            GenerateRandom(startCoords);
            return false;
        };

        Stopwatch solverTimer = new();

        solverTimer.Start();

        while(solverTimer.ElapsedMilliseconds < 150)
        {
            GenerateRandom(startCoords);
            Solver.Solver solver = new(this, startCoords, SolverSteps);
            
            if (solver.IsSolvable())
                return true;
        }
        
        solverTimer.Stop();

        return false;
    }
}