namespace LgymApi.Platform.Persistence;

internal interface IApiIdempotencyRecordDuplicateFailureClassifier
{
    bool IsDuplicateScopeAndKeyFailure(Exception commitFailure);
}
