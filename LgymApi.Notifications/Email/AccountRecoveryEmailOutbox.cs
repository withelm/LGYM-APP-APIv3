using System.Text.Json;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.Application.Notifications.Email;

internal sealed class AccountRecoveryEmailOutbox(IEmailNotificationLogRepository repository, IMapper mapper) : IAccountRecoveryEmailOutbox
{
    public Task StageAsync(AccountRecoveryEmail message, CancellationToken cancellationToken = default)
        => repository.AddAsync(mapper.Map<AccountRecoveryEmail, NotificationMessage>(message), cancellationToken);
}

internal sealed class AccountRecoveryEmailMappingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<AccountRecoveryEmail, NotificationMessage>((source, _) => new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(), Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.AccountRecovery, CorrelationId = source.CorrelationId,
            Recipient = source.RecipientEmail,
            PayloadJson = JsonSerializer.Serialize(source, SharedSerializationOptions.Current)
        });
    }
}
