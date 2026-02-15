namespace LgymApi.Domain.Security;

public static class AuthConstants
{
    public const string PermissionClaimType = "permission";

    public static class Roles
    {
        public const string User = "User";
        public const string Admin = "Admin";
        public const string Tester = "Tester";
    }
}
