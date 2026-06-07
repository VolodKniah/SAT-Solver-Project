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

        // Check if user enabled the strict empty-line option (I will be standardizing this when I add more options).
        bool strictMode = false;
        if (args.Length > 1 && (args[1] == "--strict" || args[1] == "-s"))
        {
            strictMode = true;
        }

        try
        {
            Formula formula = ParseDimacs(args[0], strictMode);
            Dictionary<int, bool> assignment = new Dictionary<int, bool>();

            Console.WriteLine("Solving...");
            bool isSatisfiable = Dpll(formula, assignment);

            if (isSatisfiable)
            {
                Console.WriteLine("SATISFIABLE");
                PrintAssignment(formula, assignment);
            }
            else
            {
                Console.WriteLine("UNSATISFIABLE");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static void PrintUsage() // I will probably make this more systematic when I learn to use the info about the terminal window size better.
    {
        Console.WriteLine("\nUsage: dotnet run <path-to-input-file> [options]\n");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <path-to-input-file>   The path to the formula file (must be passed first).\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -s, --strict           Enables strict parsing mode. Empty lines after the header");
        Console.WriteLine("                         line will be treated as empty clauses, making the");
        Console.WriteLine("                         formula UNSATISFIABLE.");
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

        // Try branching True
        assignment[chosenVar] = true;
        if (Dpll(formula, assignment))
        {
            return true;
        }

        // Try branching False
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

    // Prints the final variable assignment. I will be implementing an option that lets one print all possible assignments (the performance will be tanking, though).
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
