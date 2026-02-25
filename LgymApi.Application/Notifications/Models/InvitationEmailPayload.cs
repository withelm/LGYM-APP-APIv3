using System.Globalization;
using System.Text.Json.Serialization;

namespace LgymApi.Application.Notifications.Models;

public sealed class InvitationEmailPayload
{
    public Guid InvitationId { get; init; }
    public string InvitationCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string TrainerName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";

    [JsonIgnore]
    public CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }
}
