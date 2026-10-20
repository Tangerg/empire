# Application flow checks

From the repository root, with .NET SDK8 available:

    dotnet run --project tools/godot/application-checks -- .

Runs the real application adapters without Godot rendering. It traverses the original opening story/choice nodes, executes the native battle, verifies restart, round-trips a midbattle save, compares every remaining native state, finishes campaign result projection and checks failed-load atomicity. The native view is checked separately through the Godot scene and desktop interaction.
