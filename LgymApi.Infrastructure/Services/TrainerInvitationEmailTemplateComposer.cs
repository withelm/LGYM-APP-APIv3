using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Infrastructure.Options;
using System.Collections.Concurrent;
using System.Globalization;

namespace LgymApi.Infrastructure.Services;

public sealed class TrainerInvitationEmailTemplateComposer : IEmailTemplateComposer
{
    private readonly EmailOptions _emailOptions;
    private readonly ConcurrentDictionary<string, (string Subject, string Body)> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    public TrainerInvitationEmailTemplateComposer(EmailOptions emailOptions)
    {
        _emailOptions = emailOptions;
    }

    public EmailMessage ComposeTrainerInvitation(InvitationEmailPayload payload)
    {
        var culture = payload.Culture;
        var template = LoadTemplate("TrainerInvitation", culture);
        var baseUrl = _emailOptions.InvitationBaseUrl.TrimEnd('/');
        var acceptUrl = $"{baseUrl}/accept/{payload.InvitationId}";
        var rejectUrl = $"{baseUrl}/reject/{payload.InvitationId}";

        var expiresAt = payload.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{TrainerName}}"] = SanitizeTemplateValue(payload.TrainerName),
            ["{{InvitationCode}}"] = SanitizeTemplateValue(payload.InvitationCode),
            ["{{AcceptUrl}}"] = SanitizeTemplateValue(acceptUrl),
            ["{{RejectUrl}}"] = SanitizeTemplateValue(rejectUrl),
            ["{{ExpiresAt}}"] = expiresAt
        };

        var subject = Render(template.Subject, replacements);
        var body = Render(template.Body, replacements);

        return new EmailMessage
        {
            To = payload.RecipientEmail,
            Subject = subject,
            Body = body
        };
    }

    public EmailMessage ComposeWelcome(WelcomeEmailPayload payload)
    {
        var culture = payload.Culture;
        var template = LoadTemplate("Welcome", culture);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{UserName}}"] = SanitizeTemplateValue(payload.UserName)
        };

        var subject = Render(template.Subject, replacements);
        var body = Render(template.Body, replacements);

        return new EmailMessage
        {
            To = payload.RecipientEmail,
            Subject = subject,
            Body = body
        };
    }

    private (string Subject, string Body) LoadTemplate(string templateName, CultureInfo culture)
    {
        var templatePath = ResolveTemplatePath(templateName, culture.Name);
        if (!File.Exists(templatePath) && !string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName))
        {
            templatePath = ResolveTemplatePath(templateName, culture.TwoLetterISOLanguageName);
        }

        if (!File.Exists(templatePath))
        {
            templatePath = ResolveTemplatePath(templateName, _emailOptions.DefaultCulture.Name);
        }

        if (!File.Exists(templatePath) && !string.IsNullOrWhiteSpace(_emailOptions.DefaultCulture.TwoLetterISOLanguageName))
        {
            templatePath = ResolveTemplatePath(templateName, _emailOptions.DefaultCulture.TwoLetterISOLanguageName);
        }

        return _templateCache.GetOrAdd(templatePath, LoadTemplateFromFile);
    }

    private static (string Subject, string Body) LoadTemplateFromFile(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException($"Email template not found: {templatePath}");
        }

        var content = File.ReadAllText(templatePath);
        const string separator = "\n---\n";
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var separatorIndex = normalized.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Invalid email template format in {templatePath}");
        }

        var header = normalized[..separatorIndex].Trim();
        var body = normalized[(separatorIndex + separator.Length)..].Trim();
        const string subjectPrefix = "Subject:";
        if (!header.StartsWith(subjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Template subject header is missing in {templatePath}");
        }

        var subject = header[subjectPrefix.Length..].Trim();
        return (subject, body);
    }

    private string ResolveTemplatePath(string templateName, string cultureName)
    {
        var root = _emailOptions.TemplateRootPath;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, root);
        }

        var normalized = cultureName.Trim().ToLowerInvariant();
        return Path.Combine(root, templateName, $"{normalized}.email");
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> replacements)
    {
        var result = template;
        foreach (var replacement in replacements)
        {
            result = result.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string SanitizeTemplateValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

}
