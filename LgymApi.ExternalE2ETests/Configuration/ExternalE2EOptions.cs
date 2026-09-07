namespace LgymApi.ExternalE2ETests.Configuration;

internal sealed class ExternalE2EOptions
{
    public required Uri ApiBaseUrl { get; init; }

    public required Uri WebBaseUrl { get; init; }

    internal required string ConnectionString { get; init; }

    public required ExternalE2EConnectionIdentity ConnectionIdentity { get; init; }

    public required string BaselineDumpPath { get; init; }

    public required string ExpectedDatabaseName { get; init; }

    public required string ExpectedDatabaseMarker { get; init; }

    public required TimeSpan RestoreTimeout { get; init; }

    public required TimeSpan ApiRecoveryTimeout { get; init; }

    public required TimeSpan BrowserTimeout { get; init; }

    public required TimeSpan DiagnosticTimeout { get; init; }

    public required string ArtifactRoot { get; init; }
}

internal sealed record ExternalE2EConnectionIdentity(string Host, string DatabaseName, string UserName);
