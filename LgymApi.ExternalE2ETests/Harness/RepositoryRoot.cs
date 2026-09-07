namespace LgymApi.ExternalE2ETests.Harness;

internal static class RepositoryRoot
{
    public static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var mainSolution = Path.Combine(directory.FullName, "LgymApi.sln");
            var standaloneSolution = Path.Combine(directory.FullName, "LgymApi.ExternalE2ETests.sln");
            if (File.Exists(mainSolution) && File.Exists(standaloneSolution))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
