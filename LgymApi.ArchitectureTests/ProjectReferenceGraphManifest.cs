namespace LgymApi.ArchitectureTests;

internal static class ProjectReferenceGraphManifest
{
    public static readonly ApprovedProject[] Projects =
    [
        new("LgymApi.Api", ProjectKind.Host,
        [
            "LgymApi.Application",
            "LgymApi.BackgroundWorker",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Reporting",
            "LgymApi.Resources",
            "LgymApi.TrainingPlanning"
        ]),
        new("LgymApi.Application", ProjectKind.Production,
        [
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Platform",
            "LgymApi.Resources",
            "LgymApi.TrainingPlanning"
        ]),
        new("LgymApi.ArchitectureTests", ProjectKind.Test,
        [
            "LgymApi.Api",
            "LgymApi.Application",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Resources",
            "LgymApi.Reporting",
            "LgymApi.TrainingPlanning"
        ]),
        new("LgymApi.BackgroundWorker", ProjectKind.Host,
        [
            "LgymApi.Application",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Reporting"
        ]),
        new("LgymApi.BackgroundWorker.Common", ProjectKind.Production, ["LgymApi.Domain"]),
        new("LgymApi.DataSeeder", ProjectKind.Tooling,
        [
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure"
        ]),
        new("LgymApi.DataSeeder.Tests", ProjectKind.Test,
        [
            "LgymApi.DataSeeder",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure"
        ]),
        new("LgymApi.Domain", ProjectKind.Production, []),
        new("LgymApi.Identity", ProjectKind.Production,
        [
            "LgymApi.Domain",
            "LgymApi.Platform",
            "LgymApi.Resources"
        ]),
        new("LgymApi.Infrastructure", ProjectKind.Production,
        [
            "LgymApi.Application",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Reporting",
            "LgymApi.TrainingPlanning"
        ]),
        new("LgymApi.IntegrationTests", ProjectKind.Test,
        [
            "LgymApi.Api",
            "LgymApi.Application",
            "LgymApi.BackgroundWorker",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Resources",
            "LgymApi.Reporting",
            "LgymApi.TestUtils",
            "LgymApi.TrainingPlanning"
        ]),
        new("LgymApi.Notifications", ProjectKind.Production,
        [
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Platform",
            "LgymApi.Resources"
        ]),
        new("LgymApi.Reporting", ProjectKind.Production,
        [
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Platform",
            "LgymApi.Resources"
        ]),
        new("LgymApi.Reporting.UnitTests", ProjectKind.Test,
        [
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Platform",
            "LgymApi.Reporting",
            "LgymApi.Resources"
        ]),
        new("LgymApi.Platform", ProjectKind.Production,
        [
            "LgymApi.Domain",
            "LgymApi.Resources"
        ]),
        new("LgymApi.Resources", ProjectKind.Production, ["LgymApi.Resources.Generator"]),
        new("LgymApi.Resources.Generator", ProjectKind.Tooling, []),
        new("LgymApi.TestUtils", ProjectKind.Test,
        [
            "LgymApi.Application",
            "LgymApi.BackgroundWorker",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure"
        ]),
        new("LgymApi.TrainingPlanning", ProjectKind.Production,
        [
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Platform",
            "LgymApi.Resources"
        ]),
        new("LgymApi.UnitTests", ProjectKind.Test,
        [
            "LgymApi.Api",
            "LgymApi.Application",
            "LgymApi.BackgroundWorker",
            "LgymApi.BackgroundWorker.Common",
            "LgymApi.Domain",
            "LgymApi.Identity",
            "LgymApi.Infrastructure",
            "LgymApi.Notifications",
            "LgymApi.Platform",
            "LgymApi.Resources",
            "LgymApi.Reporting",
            "LgymApi.TestUtils",
            "LgymApi.TrainingPlanning"
        ])
    ];

    public static readonly IReadOnlyList<string> ProjectNames = Projects
        .Select(project => project.Name)
        .ToArray();

    public static readonly IReadOnlyList<string> EdgeIdentities = Projects
        .SelectMany(project => project.ApprovedDirectTargets.Select(target => $"{project.Name} -> {target}"))
        .ToArray();

    public static readonly IReadOnlySet<string> AnalyzerEdgeIdentities = new HashSet<string>(StringComparer.Ordinal)
    {
        "LgymApi.Resources -> LgymApi.Resources.Generator"
    };

    public static readonly IReadOnlySet<string> SemanticEdgeIdentities = EdgeIdentities
        .Except(AnalyzerEdgeIdentities, StringComparer.Ordinal)
        .ToHashSet(StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> ForbiddenEdgeIdentities = ProjectNames
        .SelectMany(source => ProjectNames
            .Where(target => !string.Equals(source, target, StringComparison.Ordinal))
            .Select(target => $"{source} -> {target}"))
        .Except(EdgeIdentities, StringComparer.Ordinal)
        .ToHashSet(StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> TopologicalOrder = BuildTopologicalOrder();

    private static IReadOnlyList<string> BuildTopologicalOrder()
    {
        var duplicateProjects = Projects
            .GroupBy(project => project.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateProjects.Length != 0)
        {
            throw new InvalidOperationException("Duplicate approved projects: " + string.Join(", ", duplicateProjects));
        }

        var projectNames = ProjectNames.ToHashSet(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var duplicateTargets = project.ApprovedDirectTargets
                .GroupBy(target => target, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateTargets.Length != 0)
            {
                throw new InvalidOperationException($"Duplicate approved direct targets for '{project.Name}': {string.Join(", ", duplicateTargets)}");
            }

            var unknownTargets = project.ApprovedDirectTargets
                .Where(target => !projectNames.Contains(target))
                .ToArray();
            if (unknownTargets.Length != 0)
            {
                throw new InvalidOperationException($"Unknown approved direct targets for '{project.Name}': {string.Join(", ", unknownTargets)}");
            }

            if (project.ApprovedDirectTargets.Contains(project.Name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Project '{project.Name}' cannot approve a reference to itself.");
            }
        }

        var unknownAnalyzerEdges = AnalyzerEdgeIdentities
            .Except(EdgeIdentities, StringComparer.Ordinal)
            .ToArray();
        if (unknownAnalyzerEdges.Length != 0)
        {
            throw new InvalidOperationException("Analyzer edges are not approved direct targets: " + string.Join(", ", unknownAnalyzerEdges));
        }

        var dependencyCounts = Projects.ToDictionary(
            project => project.Name,
            project => project.ApprovedDirectTargets.Count,
            StringComparer.Ordinal);
        var dependents = Projects
            .SelectMany(project => project.ApprovedDirectTargets.Select(target => (Source: project.Name, Target: target)))
            .GroupBy(edge => edge.Target, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.Source).OrderBy(source => source, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var ready = new SortedSet<string>(
            dependencyCounts.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var order = new List<string>();

        while (ready.Count != 0)
        {
            var project = ready.Min!;
            ready.Remove(project);
            order.Add(project);

            foreach (var dependent in dependents.GetValueOrDefault(project, []))
            {
                dependencyCounts[dependent]--;
                if (dependencyCounts[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (order.Count != Projects.Length)
        {
            throw new InvalidOperationException("The approved project-reference manifest contains a cycle.");
        }

        return order;
    }
}

internal sealed record ApprovedProject(
    string Name,
    ProjectKind Kind,
    IReadOnlyList<string> ApprovedDirectTargets);

internal enum ProjectKind
{
    Production,
    Host,
    Test,
    Tooling
}
