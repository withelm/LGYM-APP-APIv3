using System.Globalization;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

internal static class LocalizedReportNotificationDispatcher
{
    public static async Task DispatchAsync(
        IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger logger,
        Id<LgymApi.Domain.Entities.User> traineeId,
        Id<LgymApi.Domain.Entities.User> trainerId,
        string? templateName,
        string deliveryKey,
        string redirectUrl,
        InAppNotificationType type,
        Func<string> localizedMessageTemplateFactory,
        string logCategory,
        CancellationToken cancellationToken)
    {
        var trainer = await userRepository.FindByIdAsync(trainerId, cancellationToken);
        var trainee = await userRepository.FindByIdAsync(traineeId, cancellationToken);
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = ResolveCulture(trainee?.PreferredLanguage, appDefaultsOptions.PreferredLanguage);
            var resolvedTrainerName = string.IsNullOrWhiteSpace(trainer?.Name)
                ? global::LgymApi.Resources.Messages.GenericTrainerDisplayName
                : trainer.Name;
            var resolvedTemplateName = string.IsNullOrWhiteSpace(templateName)
                ? global::LgymApi.Resources.Messages.GenericReportDisplayName
                : templateName.Trim();

            var input = new CreateInAppNotificationInput(
                traineeId,
                trainerId,
                deliveryKey,
                false,
                string.Format(localizedMessageTemplateFactory(), resolvedTrainerName, resolvedTemplateName),
                redirectUrl,
                type);

            var result = await notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogError(
                    "Failed to create {LogCategory} notification for trainee {TraineeId}: {Error}",
                    logCategory,
                    traineeId,
                    result.Error);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static CultureInfo ResolveCulture(string? preferredLanguage, string fallbackLanguage)
    {
        var cultureName = string.IsNullOrWhiteSpace(preferredLanguage)
            ? fallbackLanguage
            : preferredLanguage;

        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(fallbackLanguage);
        }
    }
}
