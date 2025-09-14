using Microsoft.AspNetCore.Authorization;

namespace GameKeyStore.Authorization
{
    /// <summary>
    /// Authorization attribute that requires specific permission
    /// </summary>
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string resource, string action) 
            : base($"Permission.{resource}.{action}")
        {
        }
    }

    /// <summary>
    /// Common permission attributes for convenience
    /// </summary>
    public static class Permissions
    {
        public static class Games
        {
            public const string Read = "games.read";
            public const string Write = "games.write";
            public const string Delete = "games.delete";
            public const string Admin = "games.admin";
        }

        public static class Users
        {
            public const string Read = "users.read";
            public const string Write = "users.write";
            public const string Delete = "users.delete";
            public const string Admin = "users.admin";
        }

        public static class GameKeys
        {
            public const string Read = "gamekeys.read";
            public const string Write = "gamekeys.write";
            public const string Delete = "gamekeys.delete";
            public const string Admin = "gamekeys.admin";
        }

        public static class Roles
        {
            public const string Read = "roles.read";
            public const string Write = "roles.write";
            public const string Delete = "roles.delete";
            public const string Admin = "roles.admin";
        }

        public static class Orders
        {
            public const string Read = "orders.read";
            public const string Write = "orders.write";
            public const string Delete = "orders.delete";
            public const string Admin = "orders.admin";
        }
    }

    /// <summary>
    /// Convenience attributes for common permissions
    /// </summary>
    public class RequireGamesReadAttribute : RequirePermissionAttribute
    {
        public RequireGamesReadAttribute() : base("games", "read") { }
    }

    public class RequireGamesWriteAttribute : RequirePermissionAttribute
    {
        public RequireGamesWriteAttribute() : base("games", "write") { }
    }

    public class RequireGamesAdminAttribute : RequirePermissionAttribute
    {
        public RequireGamesAdminAttribute() : base("games", "admin") { }
    }

    public class RequireUsersReadAttribute : RequirePermissionAttribute
    {
        public RequireUsersReadAttribute() : base("users", "read") { }
    }

    public class RequireUsersWriteAttribute : RequirePermissionAttribute
    {
        public RequireUsersWriteAttribute() : base("users", "write") { }
    }

    public class RequireUsersAdminAttribute : RequirePermissionAttribute
    {
        public RequireUsersAdminAttribute() : base("users", "admin") { }
    }

    public class RequireGameKeysReadAttribute : RequirePermissionAttribute
    {
        public RequireGameKeysReadAttribute() : base("gamekeys", "read") { }
    }

    public class RequireGameKeysWriteAttribute : RequirePermissionAttribute
    {
        public RequireGameKeysWriteAttribute() : base("gamekeys", "write") { }
    }

    public class RequireGameKeysAdminAttribute : RequirePermissionAttribute
    {
        public RequireGameKeysAdminAttribute() : base("gamekeys", "admin") { }
    }

    public class RequireRolesReadAttribute : RequirePermissionAttribute
    {
        public RequireRolesReadAttribute() : base("roles", "read") { }
    }

    public class RequireRolesWriteAttribute : RequirePermissionAttribute
    {
        public RequireRolesWriteAttribute() : base("roles", "write") { }
    }

    public class RequireRolesAdminAttribute : RequirePermissionAttribute
    {
        public RequireRolesAdminAttribute() : base("roles", "admin") { }
    }

    // Example of custom attributes with manual resource/action definition
    public class RequireCustomResourceAttribute : RequirePermissionAttribute
    {
        public RequireCustomResourceAttribute() : base("custom-resource", "custom-action") { }
    }

    public class RequireSpecialOperationAttribute : RequirePermissionAttribute
    {
        public RequireSpecialOperationAttribute() : base("system", "special-operation") { }
    }
}
