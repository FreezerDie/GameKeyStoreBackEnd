using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using System.Security.Claims;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly PermissionService _permissionService;
        private readonly PermissionManager _permissionManager;
        private readonly ILogger<DebugController> _logger;

        public DebugController(SupabaseService supabaseService, PermissionService permissionService, PermissionManager permissionManager, ILogger<DebugController> logger)
        {
            _supabaseService = supabaseService;
            _permissionService = permissionService;
            _permissionManager = permissionManager;
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

        /// <summary>
        /// Debug endpoint to check current user's authentication status and all permissions
        /// </summary>
        [HttpGet("auth-info")]
        public async Task<IActionResult> GetAuthInfo()
        {
            try
            {
                var result = new
                {
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    AuthenticationType = User.Identity?.AuthenticationType,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                    UserInfo = await GetCurrentUserInfo(),
                    SpecificPermissionCheck = await CheckS3PresignPermission()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test the specific s3.presign permission
        /// </summary>
        [HttpGet("test-s3-permission")]
        public async Task<IActionResult> TestS3Permission()
        {
            try
            {
                var hasPermission = await CheckS3PresignPermission();
                return Ok(hasPermission);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Insert the s3.presign permission to role 1
        /// </summary>
        [HttpPost("add-s3-permissions")]
        public async Task<IActionResult> AddS3Permissions()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var s3Permissions = new[] { "s3.presign", "s3.delete" };

                var insertedCount = 0;

                foreach (var permission in s3Permissions)
                {
                    try
                    {
                        // Check if permission already exists
                        var existingResponse = await client.From<RolePermission>()
                            .Where(x => x.RoleId == 1 && x.PermissionName == permission)
                            .Get();

                        if (existingResponse.Models?.Any() == true)
                        {
                            _logger.LogInformation($"Permission {permission} already exists for role 1");
                            continue;
                        }

                        var rolePermission = new RolePermission
                        {
                            RoleId = 1,
                            PermissionName = permission,
                            GrantedAt = DateTime.UtcNow
                        };

                        await client.From<RolePermission>().Insert(rolePermission);
                        insertedCount++;
                        _logger.LogInformation($"Inserted permission {permission} for role 1");
                    }
                    catch (Exception insertEx)
                    {
                        _logger.LogWarning($"Failed to insert permission {permission}: {insertEx.Message}");
                    }
                }

                // Clear cache after adding permissions
                _permissionService.ClearAllPermissionCaches();

                return Ok(new
                {
                    message = "S3 permissions processed",
                    insertedCount = insertedCount,
                    totalRequested = s3Permissions.Length,
                    permissions = s3Permissions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding S3 permissions");
                return StatusCode(500, new
                {
                    message = "Error adding S3 permissions",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        private async Task<object> GetCurrentUserInfo()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return new { error = "No valid user ID found in claims" };
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var userResponse = await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Get();

                var user = userResponse.Models?.FirstOrDefault();
                if (user == null)
                {
                    return new { error = "User not found in database", userId = userId };
                }

                return new 
                { 
                    userId = user.Id,
                    email = user.Email,
                    username = user.Username,
                    roleId = user.RoleId,
                    isStaff = user.IsStaff
                };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }

        private async Task<object> CheckS3PresignPermission()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return new { HasPermission = false, Error = "No valid user ID found in claims" };
                }

                // Get user info
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var userResponse = await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Get();

                var user = userResponse.Models?.FirstOrDefault();
                if (user == null)
                {
                    return new { HasPermission = false, UserId = userId, Error = "User not found in database" };
                }

                if (!user.RoleId.HasValue)
                {
                    return new { HasPermission = false, UserId = userId, UserRoleId = (long?)null, Error = "User has no role assigned" };
                }

                // Get role permissions
                var rolePermissionsResponse = await client
                    .From<RolePermission>()
                    .Where(x => x.RoleId == user.RoleId.Value)
                    .Get();

                var rolePermissions = rolePermissionsResponse.Models?.ToList() ?? new List<RolePermission>();
                var permissionNames = rolePermissions.Select(rp => rp.PermissionName).ToList();

                // Check if s3.presign permission exists
                var hasS3Presign = rolePermissions.Any(rp => rp.PermissionName == "s3.presign");

                // Also check via permission service
                var hasPermissionViaService = await _permissionService.UserHasPermissionAsync(userId, "s3", "presign");

                return new 
                { 
                    HasPermission = hasPermissionViaService,
                    HasPermissionDirectCheck = hasS3Presign,
                    UserId = userId,
                    UserRoleId = user.RoleId.Value,
                    RolePermissions = permissionNames,
                    TotalRolePermissions = rolePermissions.Count
                };
            }
            catch (Exception ex)
            {
                return new { HasPermission = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Check what roles exist in the database (no auth required for debugging)
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> CheckRoles()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var rolesResponse = await client
                    .From<Role>()
                    .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var roles = rolesResponse.Models ?? new List<Role>();
                
                return Ok(new { 
                    message = "Roles retrieved from database",
                    count = roles.Count,
                    roles = roles.Select(r => new { r.Id, r.Name }).ToList(),
                    hasUserRole = roles.Any(r => r.Name.Equals("user", StringComparison.OrdinalIgnoreCase)),
                    hasAdminRole = roles.Any(r => r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase)),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Failed to check roles",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Seed roles without authentication (for debugging)
        /// </summary>
        [HttpPost("seed-roles")]
        public async Task<IActionResult> SeedRoles()
        {
            try
            {
                var success = await _permissionManager.SeedRolesAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Roles seeded successfully",
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to seed roles",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error seeding roles",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Initialize the entire permission system (seed roles and permissions)
        /// </summary>
        [HttpPost("initialize-permissions")]
        public async Task<IActionResult> InitializePermissions()
        {
            try
            {
                var success = await _permissionManager.InitializePermissionSystemAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Permission system initialized successfully",
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to initialize permission system",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error initializing permission system",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
