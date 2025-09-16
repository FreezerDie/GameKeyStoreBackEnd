using GameKeyStore.Models;

namespace GameKeyStore.Constants
{
    /// <summary>
    /// Centralized definition of all available permissions in the system
    /// This serves as the single source of truth for access control
    /// </summary>
    public static class PermissionConstants
    {
        // Resource names
        public static class Resources
        {
            public const string Users = "users";
            public const string Games = "games";
            public const string GameKeys = "gamekeys";
            public const string Categories = "categories";
            public const string Roles = "roles";
            public const string Permissions = "permissions";
            public const string Orders = "orders";
            public const string Cart = "cart";
            public const string Reports = "reports";
            public const string System = "system";
            public const string S3 = "s3";
        }

        // Action names
        public static class Actions
        {
            public const string Read = "read";
            public const string Create = "create";
            public const string Update = "update";
            public const string Delete = "delete";
            public const string Admin = "admin";
            public const string Manage = "manage";
            public const string Execute = "execute";
            public const string Presign = "presign";
        }

        // Pre-defined permission combinations
        public static class Permissions
        {
            // User Management
            public static readonly PermissionDefinition UsersRead = new(Resources.Users, Actions.Read, "users.read", "View users list and profiles");
            public static readonly PermissionDefinition UsersCreate = new(Resources.Users, Actions.Create, "users.create", "Create new users");
            public static readonly PermissionDefinition UsersUpdate = new(Resources.Users, Actions.Update, "users.update", "Update user profiles");
            public static readonly PermissionDefinition UsersDelete = new(Resources.Users, Actions.Delete, "users.delete", "Delete users");
            public static readonly PermissionDefinition UsersAdmin = new(Resources.Users, Actions.Admin, "users.admin", "Full user management access");

            // Game Management
            public static readonly PermissionDefinition GamesRead = new(Resources.Games, Actions.Read, "games.read", "View games catalog");
            public static readonly PermissionDefinition GamesCreate = new(Resources.Games, Actions.Create, "games.create", "Add new games");
            public static readonly PermissionDefinition GamesUpdate = new(Resources.Games, Actions.Update, "games.update", "Edit game details");
            public static readonly PermissionDefinition GamesDelete = new(Resources.Games, Actions.Delete, "games.delete", "Remove games from catalog");
            public static readonly PermissionDefinition GamesAdmin = new(Resources.Games, Actions.Admin, "games.admin", "Full games management access");

            // Game Keys Management
            public static readonly PermissionDefinition GameKeysRead = new(Resources.GameKeys, Actions.Read, "gamekeys.read", "View game keys");
            public static readonly PermissionDefinition GameKeysCreate = new(Resources.GameKeys, Actions.Create, "gamekeys.create", "Add new game keys");
            public static readonly PermissionDefinition GameKeysUpdate = new(Resources.GameKeys, Actions.Update, "gamekeys.update", "Update game key status");
            public static readonly PermissionDefinition GameKeysDelete = new(Resources.GameKeys, Actions.Delete, "gamekeys.delete", "Remove game keys");
            public static readonly PermissionDefinition GameKeysAdmin = new(Resources.GameKeys, Actions.Admin, "gamekeys.admin", "Full game keys management");

            // Categories Management
            public static readonly PermissionDefinition CategoriesRead = new(Resources.Categories, Actions.Read, "categories.read", "View game categories");
            public static readonly PermissionDefinition CategoriesCreate = new(Resources.Categories, Actions.Create, "categories.create", "Create new categories");
            public static readonly PermissionDefinition CategoriesUpdate = new(Resources.Categories, Actions.Update, "categories.update", "Edit categories");
            public static readonly PermissionDefinition CategoriesDelete = new(Resources.Categories, Actions.Delete, "categories.delete", "Delete categories");
            public static readonly PermissionDefinition CategoriesAdmin = new(Resources.Categories, Actions.Admin, "categories.admin", "Full categories management");

            // Role Management
            public static readonly PermissionDefinition RolesRead = new(Resources.Roles, Actions.Read, "roles.read", "View roles and permissions");
            public static readonly PermissionDefinition RolesCreate = new(Resources.Roles, Actions.Create, "roles.create", "Create new roles");
            public static readonly PermissionDefinition RolesUpdate = new(Resources.Roles, Actions.Update, "roles.update", "Edit role details");
            public static readonly PermissionDefinition RolesDelete = new(Resources.Roles, Actions.Delete, "roles.delete", "Delete roles");
            public static readonly PermissionDefinition RolesAdmin = new(Resources.Roles, Actions.Admin, "roles.admin", "Full roles management");

            // Permission Management
            public static readonly PermissionDefinition PermissionsRead = new(Resources.Permissions, Actions.Read, "permissions.read", "View available permissions");
            public static readonly PermissionDefinition PermissionsManage = new(Resources.Permissions, Actions.Manage, "permissions.manage", "Assign/revoke permissions");

            // Orders Management
            public static readonly PermissionDefinition OrdersRead = new(Resources.Orders, Actions.Read, "orders.read", "View orders");
            public static readonly PermissionDefinition OrdersCreate = new(Resources.Orders, Actions.Create, "orders.create", "Create orders");
            public static readonly PermissionDefinition OrdersUpdate = new(Resources.Orders, Actions.Update, "orders.update", "Update order status");
            public static readonly PermissionDefinition OrdersDelete = new(Resources.Orders, Actions.Delete, "orders.delete", "Cancel/delete orders");
            public static readonly PermissionDefinition OrdersAdmin = new(Resources.Orders, Actions.Admin, "orders.admin", "Full orders management");

            // Cart Management
            public static readonly PermissionDefinition CartRead = new(Resources.Cart, Actions.Read, "cart.read", "View cart items");
            public static readonly PermissionDefinition CartCreate = new(Resources.Cart, Actions.Create, "cart.create", "Add items to cart");
            public static readonly PermissionDefinition CartUpdate = new(Resources.Cart, Actions.Update, "cart.update", "Update cart items");
            public static readonly PermissionDefinition CartDelete = new(Resources.Cart, Actions.Delete, "cart.delete", "Remove items from cart");

            // Reports
            public static readonly PermissionDefinition ReportsRead = new(Resources.Reports, Actions.Read, "reports.read", "View basic reports");
            public static readonly PermissionDefinition ReportsAdmin = new(Resources.Reports, Actions.Admin, "reports.admin", "Access all reports and analytics");

            // System Administration
            public static readonly PermissionDefinition SystemAdmin = new(Resources.System, Actions.Admin, "system.admin", "Full system administration access");
            public static readonly PermissionDefinition SystemExecute = new(Resources.System, Actions.Execute, "system.execute", "Execute system operations");

            // S3 Management
            public static readonly PermissionDefinition S3Presign = new(Resources.S3, Actions.Presign, "s3.presign", "Generate presigned upload URLs");
            public static readonly PermissionDefinition S3Delete = new(Resources.S3, Actions.Delete, "s3.delete", "Delete files from S3");
        }

        /// <summary>
        /// Get all available permissions in the system
        /// </summary>
        public static List<PermissionDefinition> GetAllPermissions()
        {
            return new List<PermissionDefinition>
            {
                // User permissions
                Permissions.UsersRead,
                Permissions.UsersCreate,
                Permissions.UsersUpdate,
                Permissions.UsersDelete,
                Permissions.UsersAdmin,

                // Game permissions
                Permissions.GamesRead,
                Permissions.GamesCreate,
                Permissions.GamesUpdate,
                Permissions.GamesDelete,
                Permissions.GamesAdmin,

                // Game Keys permissions
                Permissions.GameKeysRead,
                Permissions.GameKeysCreate,
                Permissions.GameKeysUpdate,
                Permissions.GameKeysDelete,
                Permissions.GameKeysAdmin,

                // Categories permissions
                Permissions.CategoriesRead,
                Permissions.CategoriesCreate,
                Permissions.CategoriesUpdate,
                Permissions.CategoriesDelete,
                Permissions.CategoriesAdmin,

                // Roles permissions
                Permissions.RolesRead,
                Permissions.RolesCreate,
                Permissions.RolesUpdate,
                Permissions.RolesDelete,
                Permissions.RolesAdmin,

                // Permissions management
                Permissions.PermissionsRead,
                Permissions.PermissionsManage,

                // Orders permissions
                Permissions.OrdersRead,
                Permissions.OrdersCreate,
                Permissions.OrdersUpdate,
                Permissions.OrdersDelete,
                Permissions.OrdersAdmin,

                // Cart permissions
                Permissions.CartRead,
                Permissions.CartCreate,
                Permissions.CartUpdate,
                Permissions.CartDelete,

                // Reports permissions
                Permissions.ReportsRead,
                Permissions.ReportsAdmin,

                // System permissions
                Permissions.SystemAdmin,
                Permissions.SystemExecute,

                // S3 permissions
                Permissions.S3Presign,
                Permissions.S3Delete
            };
        }

        /// <summary>
        /// Get permissions grouped by resource
        /// </summary>
        public static Dictionary<string, List<PermissionDefinition>> GetPermissionsByResource()
        {
            return GetAllPermissions()
                .GroupBy(p => p.Resource)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Predefined role templates
        /// </summary>
        public static class RoleTemplates
        {
            public static readonly RoleTemplate SuperAdmin = new("Super Admin", "Full system access", new[]
            {
                Permissions.SystemAdmin,
                Permissions.UsersAdmin,
                Permissions.GamesAdmin,
                Permissions.GameKeysAdmin,
                Permissions.CategoriesAdmin,
                Permissions.RolesAdmin,
                Permissions.PermissionsManage,
                Permissions.OrdersAdmin,
                Permissions.ReportsAdmin
            });

            public static readonly RoleTemplate Admin = new("Admin", "Administrative access", new[]
            {
                Permissions.UsersRead,
                Permissions.UsersCreate,
                Permissions.UsersUpdate,
                Permissions.GamesAdmin,
                Permissions.GameKeysAdmin,
                Permissions.CategoriesAdmin,
                Permissions.OrdersAdmin,
                Permissions.ReportsRead
            });

            public static readonly RoleTemplate Manager = new("Manager", "Content and inventory management", new[]
            {
                Permissions.UsersRead,
                Permissions.GamesRead,
                Permissions.GamesCreate,
                Permissions.GamesUpdate,
                Permissions.GameKeysRead,
                Permissions.GameKeysCreate,
                Permissions.GameKeysUpdate,
                Permissions.CategoriesRead,
                Permissions.CategoriesCreate,
                Permissions.CategoriesUpdate,
                Permissions.OrdersRead,
                Permissions.OrdersUpdate
            });

            public static readonly RoleTemplate Staff = new("Staff", "Basic staff operations", new[]
            {
                Permissions.GamesRead,
                Permissions.GameKeysRead,
                Permissions.CategoriesRead,
                Permissions.OrdersRead
            });

            public static readonly RoleTemplate User = new("User", "Basic user access", new[]
            {
                Permissions.GamesRead,
                Permissions.CategoriesRead,
                Permissions.CartRead,
                Permissions.CartCreate,
                Permissions.CartUpdate,
                Permissions.CartDelete,
                Permissions.OrdersCreate,
                Permissions.OrdersRead
            });

            public static List<RoleTemplate> GetAllRoleTemplates()
            {
                return new List<RoleTemplate>
                {
                    SuperAdmin,
                    Admin,
                    Manager,
                    Staff,
                    User
                };
            }
        }
    }

    /// <summary>
    /// Represents a permission definition
    /// </summary>
    public record PermissionDefinition(string Resource, string Action, string Name, string Description)
    {
        public Permission ToPermission(long? id = null)
        {
            return new Permission
            {
                Id = id ?? 0,
                Resource = Resource,
                Action = Action,
                Name = Name,
                Description = Description
            };
        }
    }

    /// <summary>
    /// Represents a role template with predefined permissions
    /// </summary>
    public record RoleTemplate(string Name, string Description, PermissionDefinition[] Permissions);
}
