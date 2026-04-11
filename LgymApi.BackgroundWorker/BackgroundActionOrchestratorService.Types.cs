namespace LgymApi.BackgroundWorker;

public sealed partial class BackgroundActionOrchestratorService
{
    private sealed class HandlerExecutionResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public string HandlerTypeName { get; init; } = string.Empty;
        public string? ErrorDetails { get; init; }
    }
}
