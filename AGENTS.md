## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

For architecture and code relationship analysis, always use the Graphify CLI: `query`, `affected`, `god-nodes`, and `explain` as appropriate before drawing conclusions. Before changing a shared component, inspect `graphify affected "<component>"` to identify dependents. Use `graphify god-nodes` to locate architectural hubs.

The project-local CLI is `.venv/Scripts/graphify.exe` on Windows when `graphify` is not on PATH. Initialize with `.venv/Scripts/python.exe -m pip install graphifyy`, then `.venv/Scripts/graphify.exe extract . --code-only`. Keep `graphify-out/` ignored. Code-only extraction is intentional; do not run semantic extraction on the artwork or video library for routine code analysis.
