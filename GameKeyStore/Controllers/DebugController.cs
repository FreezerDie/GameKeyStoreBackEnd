using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly PermissionService _permissionService;
        private readonly ILogger<DebugController> _logger;

        public DebugController(SupabaseService supabaseService, PermissionService permissionService, ILogger<DebugController> logger)
        {
            _supabaseService = supabaseService;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// Check if permissions exist in database for role 1 (admin)
        /// </summary>
        [HttpGet("check-admin-permissions")]
        public async Task<IActionResult> CheckAdminPermissions()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Use raw SQL query to bypass model serialization issues
                var rolePermissionsQuery = @"
                    SELECT id, role_id, premission, granted_at 
                    FROM role_permissions 
                    WHERE role_id = 1";

                var usersQuery = @"
                    SELECT id, email, username, role_id, is_staff, created_at 
                    FROM users 
                    WHERE role_id = 1";

                var rolesQuery = @"
                    SELECT id, name, description 
                    FROM roles";

                // Execute raw queries
                var rolePermissionsResponse = await client.Rpc("execute_sql", new { query = rolePermissionsQuery });
                var usersResponse = await client.Rpc("execute_sql", new { query = usersQuery });
                var rolesResponse = await client.Rpc("execute_sql", new { query = rolesQuery });

                return Ok(new
                {
                    message = "Database check completed using raw queries",
                    database = new
                    {
                        rolePermissionsResponse = rolePermissionsResponse,
                        usersResponse = usersResponse,
                        rolesResponse = rolesResponse
                    },
                    expectedPermissions = new[]
                    {
                        "games.read", "games.create", "games.update", "games.delete", "games.admin",
                        "users.read", "users.create", "users.update",
                        "gamekeys.read", "gamekeys.create", "gamekeys.update", "gamekeys.delete", "gamekeys.admin",
                        "categories.read", "categories.create", "categories.update", "categories.delete", "categories.admin",
                        "orders.read", "orders.create", "orders.update", "orders.delete", "orders.admin",
                        "reports.read", "reports.admin",
                        "roles.read", "roles.create", "roles.update", "roles.delete", "roles.admin",
                        "permissions.read", "permissions.manage"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin permissions");
                
                // Try simpler approach - just check users table
                try
                {
                    await _supabaseService.InitializeAsync();
                    var client = _supabaseService.GetClient();

                    var usersResponse = await client
                        .From<User>()
                        .Where(x => x.RoleId == 1)
                        .Get();

                    var adminUsers = usersResponse.Models ?? new List<User>();

                    return Ok(new
                    {
                        message = "Fallback check - users only",
                        adminUsers = adminUsers.Select(u => new
                        {
                            id = u.Id,
                            email = u.Email,
                            username = u.Username,
                            roleId = u.RoleId,
                            isStaff = u.IsStaff
                        }).ToList(),
                        originalError = ex.Message,
                        note = "RolePermission model has serialization issues. Need to check database manually."
                    });
                }
                catch (Exception innerEx)
                {
                    return StatusCode(500, new
                    {
                        message = "Complete database error",
                        error = ex.Message,
                        innerError = innerEx.Message,
                        stackTrace = ex.StackTrace
                    });
                }
            }
        }

        /// <summary>
        /// Test permission lookup for specific user
        /// </summary>
        [HttpGet("test-permission/{userId}")]
        public async Task<IActionResult> TestUserPermission(long userId, [FromQuery] string resource = "games", [FromQuery] string action = "read")
        {
            try
            {
                // Test the permission service directly
                var hasPermission = await _permissionService.UserHasPermissionAsync(userId, resource, action);
                var userPermissions = await _permissionService.GetUserPermissionsAsync(userId);
                var rolePermissions = await _permissionService.GetRolePermissionsAsync(1); // Test role 1

                return Ok(new
                {
                    message = "Permission test completed",
                    test = new
                    {
                        userId = userId,
                        resource = resource,
                        action = action,
                        hasPermission = hasPermission
                    },
                    userPermissions = userPermissions.Select(p => new
                    {
                        name = p.Name,
                        resource = p.Resource,
                        action = p.Action
                    }).ToList(),
                    rolePermissions = rolePermissions.Select(p => new
                    {
                        name = p.Name,
                        resource = p.Resource,
                        action = p.Action
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing user permission");
                return StatusCode(500, new
                {
                    message = "Error testing permission",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Insert admin permissions directly (for testing)
        /// </summary>
        [HttpPost("insert-admin-permissions")]
        public async Task<IActionResult> InsertAdminPermissions()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var permissions = new[]
                {
                    "games.read", "games.create", "games.update", "games.delete", "games.admin",
                    "users.read", "users.create", "users.update",
                    "gamekeys.read", "gamekeys.create", "gamekeys.update", "gamekeys.delete", "gamekeys.admin",
                    "categories.read", "categories.create", "categories.update", "categories.delete", "categories.admin",
                    "orders.read", "orders.create", "orders.update", "orders.delete", "orders.admin",
                    "reports.read", "reports.admin",
                    "roles.read", "roles.create", "roles.update", "roles.delete", "roles.admin",
                    "permissions.read", "permissions.manage"
                };

                // Use direct SQL to insert permissions (bypass model issues)
                var insertValues = permissions.Select(perm => 
                    $"(1, '{perm}', NOW())").ToArray();
                
                var insertSql = $@"
                    DELETE FROM role_permissions WHERE role_id = 1;
                    INSERT INTO role_permissions (role_id, premission, granted_at) VALUES 
                    {string.Join(", ", insertValues)};";

                try
                {
                    // Try using Supabase's rpc functionality if available
                    var result = await client.Rpc("execute_sql", new { query = insertSql });
                    
                    return Ok(new
                    {
                        message = "Admin permissions inserted via SQL",
                        permissions = permissions,
                        sql = insertSql,
                        result = result
                    });
                }
                catch (Exception rpcEx)
                {
                    // Fallback: Manual insert using individual queries
                    var insertedCount = 0;
                    
                    // First delete existing
                    try
                    {
                        await client.From<RolePermission>()
                            .Where(x => x.RoleId == 1)
                            .Delete();
                    }
                    catch
                    {
                        // Ignore delete errors
                    }

                    // Try inserting one by one
                    foreach (var permission in permissions)
                    {
                        try
                        {
                            var rolePermission = new RolePermission
                            {
                                RoleId = 1,
                                PermissionName = permission,
                                GrantedAt = DateTime.UtcNow
                            };

                            await client.From<RolePermission>().Insert(rolePermission);
                            insertedCount++;
                        }
                        catch (Exception insertEx)
                        {
                            _logger.LogWarning($"Failed to insert permission {permission}: {insertEx.Message}");
                        }
                    }

                    return Ok(new
                    {
                        message = "Admin permissions inserted individually",
                        insertedCount = insertedCount,
                        totalRequested = permissions.Length,
                        permissions = permissions,
                        rpcError = rpcEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting admin permissions");
                return StatusCode(500, new
                {
                    message = "Error inserting permissions",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Clear permission cache
        /// </summary>
        [HttpPost("clear-cache")]
        public IActionResult ClearPermissionCache()
        {
            try
            {
                _permissionService.ClearAllPermissionCaches();
                return Ok(new { message = "Permission cache cleared" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error clearing cache",
                    error = ex.Message
                });
            }
        }
    }
}
