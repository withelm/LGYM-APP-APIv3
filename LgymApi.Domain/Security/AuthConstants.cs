namespace LgymApi.Domain.Security;

public static class AuthConstants
{
    public const string PermissionClaimType = "permission";

    public static class Roles
    {
        public const string User = "User";
        public const string Admin = "Admin";
        public const string Tester = "Tester";
        public const string Trainer = "Trainer";
    }

    public static class Permissions
    {
        public const string AdminAccess = "admin.access";
        public const string ManageUserRoles = "users.roles.manage";
        public const string ManageAppConfig = "appconfig.manage";
        public const string ManageGlobalExercises = "exercises.global.manage";

        public static readonly IReadOnlyList<string> All =
        [
            AdminAccess,
            ManageUserRoles,
            ManageAppConfig,
            ManageGlobalExercises
        ];
    }

    public static class Policies
    {
        public const string ManageUserRoles = "policy.users.roles.manage";
        public const string ManageAppConfig = "policy.appconfig.manage";
        public const string ManageGlobalExercises = "policy.exercises.global.manage";
        public const string TrainerAccess = "policy.trainer.access";
    }
}
