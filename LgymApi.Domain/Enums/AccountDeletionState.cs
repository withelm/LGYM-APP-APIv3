namespace LgymApi.Domain.Enums;

public enum AccountDeletionState
{
    Active = 0,
    PendingDeletion = 1,
    Finalizing = 2,
    PermanentlyDeleted = 3
}
