# DPLL SAT Solver
Usage: dotnet run <path-to-input-file> [options]

Arguments:
  <path-to-input-file>     The path to the formula file (must be passed first).

Options:
  -s, --strict:             Enables strict parsing mode. Empty lines after the
                           header line will be treated as empty clauses, making the
                           formula UNSATISFIABLE.

--all:                      Enables full solver mode. All truth assignments will be
                            printed. Performance may take a hit.
