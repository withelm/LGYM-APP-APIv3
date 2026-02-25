using System.Globalization;
using System.Text.Json.Serialization;

namespace LgymApi.Application.Notifications.Models;

public sealed class WelcomeEmailPayload
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
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
