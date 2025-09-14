using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionsController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly PermissionService _permissionService;

        public PermissionsController(SupabaseService supabaseService, PermissionService permissionService)
        {
            _supabaseService = supabaseService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Get all permissions from the database
        /// </summary>
        /// <returns>List of permissions</returns>
        [HttpGet]
        [RequireRolesRead]
        public async Task<IActionResult> GetPermissions()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                var response = await client
                    .From<Permission>()
                    .Order(x => x.Resource!, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                var permissionDtos = response.Models?.Select(x => x.ToDto()).ToList();
                
                return Ok(new { 
                    message = "Permissions fetched from database", 
                    count = permissionDtos?.Count ?? 0,
                    data = permissionDtos
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                // Return default permissions as fallback
                var defaultPermissions = new List<PermissionDto>
                {
                    new PermissionDto { Id = 1, Name = "games.read", Description = "Read games", Resource = "games", Action = "read" },
                    new PermissionDto { Id = 2, Name = "games.write", Description = "Create/update games", Resource = "games", Action = "write" },
                    new PermissionDto { Id = 3, Name = "games.admin", Description = "Full games management", Resource = "games", Action = "admin" },
                    new PermissionDto { Id = 4, Name = "users.read", Description = "Read users", Resource = "users", Action = "read" },
                    new PermissionDto { Id = 5, Name = "users.write", Description = "Create/update users", Resource = "users", Action = "write" },
                    new PermissionDto { Id = 6, Name = "users.admin", Description = "Full user management", Resource = "users", Action = "admin" },
                    new PermissionDto { Id = 7, Name = "gamekeys.read", Description = "Read game keys", Resource = "gamekeys", Action = "read" },
                    new PermissionDto { Id = 8, Name = "gamekeys.write", Description = "Create/update game keys", Resource = "gamekeys", Action = "write" },
                    new PermissionDto { Id = 9, Name = "gamekeys.admin", Description = "Full game keys management", Resource = "gamekeys", Action = "admin" },
                    new PermissionDto { Id = 10, Name = "roles.read", Description = "Read roles", Resource = "roles", Action = "read" },
                    new PermissionDto { Id = 11, Name = "roles.write", Description = "Create/update roles", Resource = "roles", Action = "write" },
                    new PermissionDto { Id = 12, Name = "roles.admin", Description = "Full roles management", Resource = "roles", Action = "admin" }
                };
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = defaultPermissions.Count,
                    data = defaultPermissions
                });
            }
        }

        /// <summary>
        /// Get permissions for a specific role
        /// </summary>
        /// <param name="roleId">Role ID</param>
        /// <returns>List of role permissions</returns>
        [HttpGet("role/{roleId}")]
        [RequireRolesRead]
        public async Task<IActionResult> GetRolePermissions(long roleId)
        {
            try
            {
                var permissions = await _permissionService.GetRolePermissionsAsync(roleId);
                var permissionDtos = permissions.Select(p => p.ToDto()).ToList();

                return Ok(new { 
                    message = $"Permissions for role {roleId} fetched from database", 
                    roleId = roleId,
                    count = permissionDtos.Count,
                    data = permissionDtos
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching role permissions", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get role with its permissions
        /// </summary>
        /// <param name="roleId">Role ID</param>
        /// <returns>Role with permissions</returns>
        [HttpGet("role/{roleId}/details")]
        [RequireRolesRead]
        public async Task<IActionResult> GetRoleWithPermissions(long roleId)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Get role
                var roleResponse = await client
                    .From<Role>()
                    .Where(x => x.Id == roleId)
                    .Get();

                var role = roleResponse.Models?.FirstOrDefault();
                if (role == null)
                {
                    return NotFound(new { message = $"Role with ID {roleId} not found" });
                }

                // Get role permissions
                var permissions = await _permissionService.GetRolePermissionsAsync(roleId);
                
                var roleWithPermissions = new RoleWithPermissionsDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    Permissions = permissions.Select(p => p.ToDto()).ToList()
                };

                return Ok(new { 
                    message = "Role with permissions found", 
                    data = roleWithPermissions
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching role details", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Add permission to role
        /// </summary>
        /// <param name="roleId">Role ID</param>
        /// <param name="permissionName">Permission name (e.g., "games.read")</param>
        /// <returns>Success/failure result</returns>
        [HttpPost("role/{roleId}/permission/{permissionName}")]
        [RequireRolesWrite]
        public async Task<IActionResult> AddPermissionToRole(long roleId, string permissionName)
        {
            try
            {
                var result = await _permissionService.AddPermissionToRoleAsync(roleId, permissionName);
                
                if (result)
                {
                    return Ok(new { 
                        message = "Permission added to role successfully",
                        roleId = roleId,
                        permissionName = permissionName
                    });
                }
                else
                {
                    return BadRequest(new { message = "Failed to add permission to role" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error adding permission to role", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Remove permission from role
        /// </summary>
        /// <param name="roleId">Role ID</param>
        /// <param name="permissionName">Permission name (e.g., "games.read")</param>
        /// <returns>Success/failure result</returns>
        [HttpDelete("role/{roleId}/permission/{permissionName}")]
        [RequireRolesWrite]
        public async Task<IActionResult> RemovePermissionFromRole(long roleId, string permissionName)
        {
            try
            {
                var result = await _permissionService.RemovePermissionFromRoleAsync(roleId, permissionName);
                
                if (result)
                {
                    return Ok(new { 
                        message = "Permission removed from role successfully",
                        roleId = roleId,
                        permissionName = permissionName
                    });
                }
                else
                {
                    return BadRequest(new { message = "Failed to remove permission from role" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error removing permission from role", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Check if user has specific permission
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="resource">Resource name</param>
        /// <param name="action">Action name</param>
        /// <returns>Permission check result</returns>
        [HttpGet("user/{userId}/check")]
        [RequireUsersRead]
        public async Task<IActionResult> CheckUserPermission(long userId, [FromQuery] string resource, [FromQuery] string action)
        {
            try
            {
                if (string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(action))
                {
                    return BadRequest(new { message = "Resource and action parameters are required" });
                }

                var hasPermission = await _permissionService.UserHasPermissionAsync(userId, resource, action);
                
                return Ok(new { 
                    message = "Permission check completed",
                    userId = userId,
                    resource = resource,
                    action = action,
                    hasPermission = hasPermission
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error checking user permission", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get all permissions for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of user permissions</returns>
        [HttpGet("user/{userId}")]
        [RequireUsersRead]
        public async Task<IActionResult> GetUserPermissions(long userId)
        {
            try
            {
                var permissions = await _permissionService.GetUserPermissionsAsync(userId);
                var permissionDtos = permissions.Select(p => p.ToDto()).ToList();

                return Ok(new { 
                    message = $"Permissions for user {userId} fetched from database", 
                    userId = userId,
                    count = permissionDtos.Count,
                    data = permissionDtos
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching user permissions", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Clear permission caches (Admin only)
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("cache/clear")]
        [RequireRolesAdmin]
        public IActionResult ClearPermissionCaches()
        {
            try
            {
                _permissionService.ClearAllPermissionCaches();
                
                return Ok(new { 
                    message = "Permission caches cleared successfully" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error clearing permission caches", 
                    error = ex.Message 
                });
            }
        }
    }
}
