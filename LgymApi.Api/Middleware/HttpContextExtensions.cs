using LgymApi.Domain.Entities;

namespace LgymApi.Api.Middleware;

public static class HttpContextExtensions
{
    public static User? GetCurrentUser(this HttpContext context)
    {
        if (context.Items.TryGetValue("User", out var user) && user is User typedUser)
        {
            return typedUser;
        }

        return null;
    }
}
