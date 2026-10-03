
# C# Coding Standards

- Functions should be pure and return a value or data structure, or if they mutate state should return `void`, always prefer pure functional style over imperative mutations.
- Loops (especially nested foreach) should be refactored into LINQ statements to aid with readability and debuggability. The code should first produce results in one operation, then iterate on the results.
- Comments should follow principle of least surprise, no history or decision process in the comment, only something the reader could not deduce from the context and the code. 99% of inline comments should just be deleted, a good function name and clean code wins.
- All I/O and state storage operations (disk, network, database, config) should always be abstracted to a service interface for testability.
- Unit tests should structured to have one folder per subject class under test:
   - For stateful classes there one test class per scenario, a single setup call should initialise the classes state a and the test methods should perform the assert only.
   - For pure functions / extensions etc.
- Extension methods should be used for generic and interface types, move all extensions that operate on a single class into the class itself unless it is not modifiable (ie. it is in a third party library).
- Prefer extension methods over stateless wrapper / utility classes.
- Intermediate classes should be strongly named immutable record classes.
- Performance critical code should prefer `ReadOnlySpan<T>`, `Span<T>`, `ReadOnlyMemory<T>` or `Memory<T>` over arrays or lists as parameters.
- Performance critical code should be tested using Benchmark.NET, this is non-negotiable.
- Code structure should always be IoC constructible and unit testable.
- No public static mutable state, EVER. Use a singleton pattern if necessary for static readonly data that will never change, constructor injection of configuration should be preferred.


# Documentation Standards

- Use [ADR standard](docs/architecture/README.md) for all architectural design decision planning, agents can only propose and explore design decisions, only a human edit may change status to Accepted.