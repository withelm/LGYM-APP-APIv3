namespace LgymApi.Domain.Enums;

public enum OutboxDeliveryStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3
}
