using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Application.Notifications.Models;

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

        configuration.CreateMap<GetNotificationsQueryDto, CursorPaginationQuery>((source, _) =>
            new CursorPaginationQuery(
                source.Limit,
                source.CursorCreatedAt,
                !string.IsNullOrWhiteSpace(source.CursorId) && Id<User>.TryParse(source.CursorId, out var cursorId)
                    ? cursorId
                    : null));

        configuration.CreateMap<PagedResult<InAppNotificationResult>, PagedNotificationsResultDto>((source, context) =>
            new PagedNotificationsResultDto(
                Items: context!.MapList<InAppNotificationResult, InAppNotificationResultDto>(source.Items),
                HasNextPage: source.HasNextPage,
                NextCursorCreatedAt: source.NextCursorCreatedAt,
                NextCursorId: source.NextCursorId?.ToString()));

        configuration.CreateMap<int, UnreadCountDto>((source, _) => new UnreadCountDto(source));
    }
}
