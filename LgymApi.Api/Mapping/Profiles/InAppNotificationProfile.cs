using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Notifications.Application.Models;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class InAppNotificationProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<InAppNotificationResult, InAppNotificationResultDto>((source, _) =>
            new InAppNotificationResultDto(
                Id: source.Id.ToString(),
                Message: source.Message,
                RedirectUrl: source.RedirectUrl,
                IsRead: source.IsRead,
                Type: source.Type.Value,
                IsSystemNotification: source.IsSystemNotification,
                SenderUserId: source.SenderUserId.HasValue ? source.SenderUserId.Value.ToString() : null,
                CreatedAt: source.CreatedAt));

        configuration.CreateMap<PagedResult<InAppNotificationResult>, PagedNotificationsResultDto>((source, context) =>
            new PagedNotificationsResultDto(
                Items: context!.MapList<InAppNotificationResult, InAppNotificationResultDto>(source.Items),
                HasNextPage: source.HasNextPage,
                NextCursorCreatedAt: source.NextCursorCreatedAt,
                NextCursorId: source.NextCursorId));
    }
}
