using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using System.Security.Claims;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SimpleDebugController : ControllerBase
    {
        private readonly PermissionService _permissionService;
        private readonly AuthService _authService;
        private readonly ILogger<SimpleDebugController> _logger;

        public SimpleDebugController(PermissionService permissionService, AuthService authService, ILogger<SimpleDebugController> logger)
        {
            _permissionService = permissionService;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Test permission lookup for your specific user (bypassing model issues)
        /// </summary>
        [HttpGet("test-my-permissions")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> TestMyPermissions()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Test the permission check directly
                var hasGamesRead = await _permissionService.UserHasPermissionAsync(userId, "games", "read");
                var hasUsersRead = await _permissionService.UserHasPermissionAsync(userId, "users", "read");
                var hasGamesAdmin = await _permissionService.UserHasPermissionAsync(userId, "games", "admin");

                // Get user info
                var user = await _authService.GetUserByIdAsync(userId);

                // Try to get role permissions (this might fail due to model issues)
                var rolePermissions = new List<object>();
                try
                {
                    var permissions = await _permissionService.GetRolePermissionsAsync(user?.RoleId ?? 0);
                    rolePermissions = permissions.Select(p => new {
                        name = p.Name,
                        resource = p.Resource,
                        action = p.Action
                    }).ToList<object>();
                }
                catch (Exception ex)
                {
                    rolePermissions.Add(new { error = ex.Message });
                }

                return Ok(new
                {
                    message = "Permission test completed",
                    user = new
                    {
                        id = userId,
                        email = user?.Email,
                        username = user?.Username,
                        roleId = user?.RoleId,
                        isStaff = user?.IsStaff
                    },
                    permissionTests = new
                    {
                        hasGamesRead = hasGamesRead,
                        hasUsersRead = hasUsersRead,
                        hasGamesAdmin = hasGamesAdmin
                    },
                    rolePermissions = rolePermissions,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing permissions");
                return StatusCode(500, new
                {
                    message = "Error testing permissions",
                    error = ex.Message
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
                return Ok(new { message = "Permission cache cleared successfully" });
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
        /// Get basic system info without problematic models
        /// </summary>
        [HttpGet("system-info")]
        public IActionResult GetSystemInfo()
        {
            return Ok(new
            {
                message = "System debug information",
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
                },
                notes = new[]
                {
                    "RolePermission model has serialization issues with Supabase",
                    "Use direct SQL insertion in your database: insert_admin_permissions.sql",
                    "After inserting permissions, clear cache and test again"
                }
            });
        }
    }
}
