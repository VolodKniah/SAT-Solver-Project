// Represents a literal (a variable or its negation).
// Positive numbers = variable X, Negative numbers = NOT variable X.
public class Literal
{
    public int Variable;
    public bool IsPositive;

    public Literal(int rawValue)
    {
        this.Variable = Math.Abs(rawValue);
        this.IsPositive = rawValue > 0;
    }

    public override string ToString()
    {
        return this.IsPositive ? this.Variable.ToString() : "-" + this.Variable;
    }
}

// Represents a clause.
public class Clause
{
    public List<Literal> Literals = new List<Literal>();

    // Evaluates the clause given current variable assignments.
    // Returns true if satisfied, false if unsatisfied (all false), null if unresolved.
    public bool? Evaluate(Dictionary<int, bool> assignment)
    {
        bool hasUnassigned = false;

        foreach (Literal lit in this.Literals)
        {
            if (assignment.ContainsKey(lit.Variable))
            {
                bool val = assignment[lit.Variable];
                if (lit.IsPositive == val)
                {
                    return true;
                }
            }
            else
            {
                hasUnassigned = true;
            }
        }

        if (hasUnassigned)
        {
            return null;
        }

        return false;
    }
}

// Represents the full conjunction of clauses.
public class Formula
{
    public List<Clause> Clauses = new List<Clause>();
    public List<int> Variables = new List<int>();
}

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        string filePath = null;
        bool strictMode = false;
        bool findAllSolutions = false;
        
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("-"))
            {
                if (filePath == null)
                {
                    Console.WriteLine("Error: Invalid argument order. The input file path must precede any optional flags.");
                    PrintUsage();
                    return;
                }

                switch (arg)
                {
                    case "-s":
                    case "--strict":
                        strictMode = true;
                        break;

                    case "--all":
                        findAllSolutions = true;
                        break;

                    default:
                        Console.WriteLine("Error: Unknown option '" + arg + "'");
                        PrintUsage();
                        return;
                }
            }
            else
            {
                if (filePath == null)
                {
                    filePath = arg;
                }
                else
                {
                    Console.WriteLine("Error: Unexpected extra argument '" + arg + "'");
                    PrintUsage();
                    return;
                }
            }
        }

        if (filePath == null)
        {
            Console.WriteLine("Error: No input file specified.");
            PrintUsage();
            return;
        }

        try
        {
            Formula formula = ParseDimacs(args[0], strictMode);
            int solutionCount = 0;
            bool keepSolving = true;
            // Run initial check for satisfiability.
            Dictionary<int, bool> firstCheckAssignment = new Dictionary<int, bool>();
            bool initiallySatisfiable = Dpll(formula, firstCheckAssignment);

            Console.WriteLine("Solving...");

            if (!initiallySatisfiable)
            {
                Console.WriteLine("UNSATISFIABLE");
                return;
            }

            while (keepSolving)
            {
                Dictionary<int, bool> assignment = new Dictionary<int, bool>();
                
                bool isSatisfiable = Dpll(formula, assignment);

                if (isSatisfiable)
                {
                    solutionCount++;
                    Console.Write("Solution #" + solutionCount + ": ");
                    PrintAssignment(formula, assignment);

                    if (!findAllSolutions)
                    {
                        keepSolving = false;
                        break;
                    }

                    // Create a new blocking clause to outlaw this exact assignment.
                    Clause blockingClause = new Clause();

                    foreach (int v in formula.Variables)
                    {
                        bool val = false;
                        if (assignment.ContainsKey(v))
                        {
                            val = assignment[v];
                        }
                        else
                        {
                            assignment[v] = false; 
                        }
                        // Invert the polarity for the blocking clause
                        int rawLiteralValue = val ? -v : v;
                        blockingClause.Literals.Add(new Literal(rawLiteralValue));
                    }

                    // If the blocking clause ended up completely empty, break to avoid infinite loops.
                    if (blockingClause.Literals.Count == 0)
                    {
                        break;
                    }

                    // Append the new constraint directly into the live formula.
                    formula.Clauses.Add(blockingClause);
                }
                else
                {
                    keepSolving = false; 
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static void PrintUsage()
    {
        // Fallback padding for the description column.
        int indentationWidth = 27; 

        Console.WriteLine();
        Console.WriteLine("Usage: dotnet run <path-to-input-file> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        
        PrintWrapped(
            "  <path-to-input-file>", 
            "The path to the formula file (must be passed first).", 
            indentationWidth
        );
        Console.WriteLine();

        Console.WriteLine("Options:");
        
        PrintWrapped(
            "  -s, --strict", 
            "Enables strict parsing mode. Empty lines after the header line will be treated as empty clauses, making the formula UNSATISFIABLE.", 
            indentationWidth
        );
        Console.WriteLine();

        PrintWrapped(
            "--all", 
            "Enables full solver mode. All truth assignments will be printed. Performance may take a hit.", 
            indentationWidth
        );
        Console.WriteLine();
    }

    // Helper method to wrap text based on live terminal width.
    static void PrintWrapped(string prefix, string description, int indentSize)
    {
        // Get the dynamic width of the terminal. 
        // Fallback to a standard 80 columns if the environment doesn't expose it.
        int terminalWidth = 80;
        try { terminalWidth = Console.WindowWidth; } catch { }

        // Calculate how much space we actually have left for text.
        int maxTextWidth = terminalWidth - indentSize;
        if (maxTextWidth < 20) maxTextWidth = 20; // Safety floor for tiny windows.

        if (prefix.Length >= indentSize)
        {
            Console.WriteLine(prefix);
            Console.Write(new string(' ', indentSize));
        }
        else
        {
            Console.Write(prefix.PadRight(indentSize));
        }

        string[] words = description.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int currentLineLength = 0;

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];

            if (currentLineLength + word.Length + 1 > maxTextWidth)
            {
                Console.WriteLine();
                Console.Write(new string(' ', indentSize));
                currentLineLength = 0;
            }

            Console.Write(word + " ");
            currentLineLength += word.Length + 1;
        }
        Console.WriteLine();
    }

    // Core DPLL Algorithm.
    static bool Dpll(Formula formula, Dictionary<int, bool> assignment)
    {
        // 1. Check current status of the formula.
        bool allSatisfied = true;
        foreach (Clause c in formula.Clauses)
        {
            bool? status = c.Evaluate(assignment);
            if (status == false) 
            {
                return false;
            }
            if (status == null)
            {
                allSatisfied = false;
            }
        }

        if (allSatisfied)
        {
            return true;
        }

        // 2. Unit Propagation.
        Literal unitLit = FindUnitClause(formula, assignment);
        if (unitLit != null)
        {
            assignment[unitLit.Variable] = unitLit.IsPositive;
            bool result = Dpll(formula, assignment);
            if (!result)
            {
                assignment.Remove(unitLit.Variable); // Backtrack
            }
            return result;
        }

        // 3. Pure Literal Elimination.
        Literal pureLit = FindPureLiteral(formula, assignment);
        if (pureLit != null)
        {
            assignment[pureLit.Variable] = pureLit.IsPositive;
            bool result = Dpll(formula, assignment);
            if (!result)
            {
                assignment.Remove(pureLit.Variable); // Backtrack
            }
            return result;
        }

        // 4. Choosing a branching literal.
        int chosenVar = -1;
        foreach (int v in formula.Variables)
        {
            if (!assignment.ContainsKey(v))
            {
                chosenVar = v;
                break;
            }
        }

        if (chosenVar == -1) return false; // All variables have a truth assignment, but AllSatisfied check earlier didn't succeed, so the formula is UNSAT.

        // Try branching True.
        assignment[chosenVar] = true;
        if (Dpll(formula, assignment))
        {
            return true;
        }

        // Try branching False.
        assignment[chosenVar] = false;
        if (Dpll(formula, assignment))
        {
            return true;
        }

        // Both branches failed, clean up assignment and backtrack.
        assignment.Remove(chosenVar);
        return false;
    }

    // Finds a clause where exactly one literal is unassigned and all others are false.
    static Literal FindUnitClause(Formula formula, Dictionary<int, bool> assignment)
    {
        foreach (Clause c in formula.Clauses)
        {
            if (c.Evaluate(assignment) != null) continue;

            Literal unassignedLit = null;
            int unassignedCount = 0;

            foreach (Literal lit in c.Literals)
            {
                if (!assignment.ContainsKey(lit.Variable))
                {
                    unassignedCount++;
                    unassignedLit = lit;
                }
            }

            if (unassignedCount == 1)
            {
                return unassignedLit;
            }
        }
        return null;
    }

    // Finds a literal that only appears with one polarity across all unresolved clauses
    static Literal FindPureLiteral(Formula formula, Dictionary<int, bool> assignment)
    {
        Dictionary<int, bool> seenPolarity = new Dictionary<int, bool>();
        HashSet<int> pureVariables = new HashSet<int>();

        foreach (Clause c in formula.Clauses)
        {
            if (c.Evaluate(assignment) == true) continue; // Ignore already satisfied clauses

            foreach (Literal lit in c.Literals)
            {
                if (assignment.ContainsKey(lit.Variable)) continue;

                if (seenPolarity.ContainsKey(lit.Variable))
                {
                    if (seenPolarity[lit.Variable] != lit.IsPositive)
                    {
                        pureVariables.Remove(lit.Variable);
                    }
                }
                else
                {
                    seenPolarity[lit.Variable] = lit.IsPositive;
                    pureVariables.Add(lit.Variable);
                }
            }
        }

        // Return the first valid pure literal found (best to run the function again instead of looking further, since the amount of clauses may have shrinked).
        foreach (int v in pureVariables)
        {
            Literal pureLit = new Literal(v);
            pureLit.IsPositive = seenPolarity[v];
            return pureLit;
        }

        return null;
    }

    // DIMACS Parser.
    static Formula ParseDimacs(string path, bool strictMode)
    {
        Formula formula = new Formula();
        HashSet<int> uniqueVars = new HashSet<int>();
        bool headerPassed = false;

        using (StreamReader reader = new StreamReader(path))
        {
            string line;
            Clause currentClause = new Clause();

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                // Skip comments entirely
                if (line.StartsWith("c"))
                {
                    continue;
                }

                // Detect and mark the problem header line. Regrettably, the solver does not currently use any information it provides.
                if (line.StartsWith("p"))
                {
                    headerPassed = true;
                    continue;
                }

                // Handle empty lines.
                if (line == "")
                {
                    // Empty lines only interpreted as clauses if run with -s or --strict option. Empty lines before the header are ignored.
                    if (strictMode && headerPassed)
                    {
                        formula.Clauses.Add(new Clause()); 
                    }
                    continue;
                }

                headerPassed = true; // Assume the header is passed once we hit the content lines, even if it was omitted.

                string[] tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in tokens)
                {
                    int value = int.Parse(token);
                    if (value == 0)
                    {
                        formula.Clauses.Add(currentClause);
                        currentClause = new Clause();
                    }
                    else
                    {
                        Literal lit = new Literal(value);
                        currentClause.Literals.Add(lit);
                        uniqueVars.Add(lit.Variable);
                    }
                }
            }
            
            // Catch any trailing tokens left over (see sat.cnf).
            if (currentClause.Literals.Count > 0)
            {
                formula.Clauses.Add(currentClause);
            }
        }

        formula.Variables.AddRange(uniqueVars);
        formula.Variables.Sort();
        return formula;
    }

    static void PrintAssignment(Formula formula, Dictionary<int, bool> assignment)
    {
        List<string> result = new List<string>();
        foreach (int v in formula.Variables)
        {
            bool val = false;
            if (assignment.ContainsKey(v))
            {
                val = assignment[v];
            }
            result.Add(val ? v.ToString() : "-" + v);
        }
        Console.WriteLine(string.Join(" ", result.ToArray()) + " 0");
    }
}
