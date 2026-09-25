using sharpmines.Game;

namespace sharpmines.Solver;

public class Solver
{
    private readonly Board SolvingBoard;
    private readonly (int y, int x) StartCoords;
    private readonly int MaxSteps;
    private readonly HashSet<(int y, int x)> ActiveClues;

    public Solver(Board board, (int y, int x) startCoors, int maxSteps)
    {
        SolvingBoard = new(board);
        StartCoords = startCoors;
        MaxSteps = maxSteps;
        ActiveClues = SolvingBoard.FloodUncoverClues(StartCoords);
    }

    public bool IsSolvable()
    {
        HashSet<(int y, int x)> activeClues = SolvingBoard.FloodUncoverClues(StartCoords);

        for (int i = 0; i < MaxSteps; i++)
        {
            if (!TakeStep(activeClues)) return false;
            if (SolvingBoard.IsCleared()) return true;
        }

        return SolvingBoard.IsCleared();
    }

    private bool TakeStep(HashSet<(int y, int x)> activeClues)
    {
        bool progress = false;
        
        var cluesToProcess = activeClues.ToList();
        
        HashSet<(int y, int x)> newClues = new();

        foreach ((int y, int x) coords in cluesToProcess)
        {
            // RenderBoard(board, (y, x));

            IEnumerable<(int y, int x)> neighbors = SolvingBoard.GetNeighborCoords(coords).ToList();
            List<(int y, int x)> hidden = neighbors.Where(
                n => SolvingBoard.IsCovered(n) && !SolvingBoard.IsFlagged(n)
            ).ToList();
            
            // Clue is spent
            if (hidden.Count == 0)
            {
                activeClues.Remove(coords);
                continue; // Stop processing this tile
            }
            
            int flags = neighbors.Count(n => SolvingBoard.IsFlagged(n));
            int effectiveCount = SolvingBoard.GetCount(coords) - flags;

            // All remaining hidden neighbors are mines
            if (hidden.Count == effectiveCount)
            {
                foreach ((int hy, int hx) hiddenCoords in hidden)
                {
                    SolvingBoard.Flag(hiddenCoords);
                    progress = true;
                }
                // Once flagged, this tile has no remaining unflagged hidden neighbors
                activeClues.Remove(coords);
            }
            // All remaining hidden neighbors are safe -> Cascade open
            else if (effectiveCount == 0)
            {
                foreach ((int ny, int nx) hiddenCoords in hidden)
                {
                    newClues.UnionWith(SolvingBoard.FloodUncoverClues(hiddenCoords));
                }
                activeClues.Remove(coords);
                progress = true;
            }
        }

        activeClues.UnionWith(newClues);

        // Fallback pass if single-cell logic made no progress
        if (!progress)
        {
            // Pass activeClues to DeduceSubsets so it doesn't scan the whole board
            progress = DeduceSubsets(activeClues);
        }

        return progress;
    }

    bool DeduceSubsets(HashSet<(int y, int x)> activeClues)
    {
        bool progress = false;
        var equations = new List<(List<(int y, int x)> hidden, int eff)>();

        foreach ((int y, int x) clueCoords in activeClues)
        {
            List<(int, int)> neighbors = SolvingBoard.GetNeighborCoords(clueCoords).ToList();
            List<(int, int)> hidden = neighbors.Where(n => SolvingBoard.IsCovered(n) && !SolvingBoard.IsFlagged(n)).ToList();
            int flags = neighbors.Count(SolvingBoard.IsFlagged);
            int effectiveCount = SolvingBoard.GetCount(clueCoords) - flags;

            if (hidden.Count > 0)
                equations.Add((hidden, effectiveCount));
        }

        HashSet<(int y, int x)> newClues = new();

        foreach ((List<(int, int)> hidden, int eff) eq1 in equations)
        {
            foreach ((List<(int, int)> hidden, int eff) eq2 in equations)
            {
                if (ReferenceEquals(eq1.hidden, eq2.hidden)) continue;

                // Check if eq1 is a strict subset of eq2
                if (eq2.hidden.All(eq2.hidden.Contains))
                {
                    var diff = eq2.hidden.Where(h => !eq2.hidden.Contains(h)).ToList();
                    int diffEff = eq2.eff - eq1.eff;

                    if (diff.Count > 0)
                    {
                        // Difference tiles are 100% safe
                        if (diffEff == 0)
                        {
                            foreach ((int, int) diffCoords in diff)
                            {
                                if (SolvingBoard.IsCovered(diffCoords))
                                {
                                    newClues.UnionWith(SolvingBoard.FloodUncoverClues(diffCoords));
                                    progress = true;
                                }
                            }
                        }
                        // Difference tiles are 100% mines
                        else if (diffEff == diff.Count)
                        {
                            foreach ((int, int) diffCoords in diff)
                            {
                                if (!SolvingBoard.IsFlagged(diffCoords))
                                {
                                    SolvingBoard.Flag(diffCoords);
                                    progress = true;
                                }
                            }
                        }
                    }
                }
            }
            if (progress) break;
        }

        activeClues.UnionWith(newClues);

        return progress;
    }

}