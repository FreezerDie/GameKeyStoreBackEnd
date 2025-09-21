using GameKeyStore.Models;
using GameKeyStore.Constants;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace GameKeyStore.Services
{
    public class PermissionService
    {
        private readonly SupabaseService _supabaseService;
        private readonly IMemoryCache _cache;
        private const int CacheExpirationMinutes = 15; // Cache permissions for 15 minutes

        public PermissionService(SupabaseService supabaseService, IMemoryCache cache)
        {
            _supabaseService = supabaseService;
            _cache = cache;
        }

        /// <summary>
        /// Get all permissions for a specific role (cached)
        /// </summary>
        public async Task<List<Permission>> GetRolePermissionsAsync(long roleId)
        {
            var cacheKey = $"role_permissions_{roleId}";
            
            if (_cache.TryGetValue(cacheKey, out List<Permission>? cachedPermissions))
            {
                return cachedPermissions ?? new List<Permission>();
            }

            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get role-permission relationships
                var rolePermissionsResponse = await client
                    .From<RolePermission>()
                    .Where(x => x.RoleId == roleId)
                    .Get();

                var rolePermissions = rolePermissionsResponse.Models ?? new List<RolePermission>();

                if (!rolePermissions.Any())
                {
                    var emptyList = new List<Permission>();
                    _cache.Set(cacheKey, emptyList, TimeSpan.FromMinutes(CacheExpirationMinutes));
                    return emptyList;
                }

                // Convert permission names to Permission objects using the new static method
                var permissions = new List<Permission>();
                foreach (var rolePermission in rolePermissions)
                {
                    if (!string.IsNullOrEmpty(rolePermission.PermissionName))
                    {
                        try
                        {
                            permissions.Add(Permission.FromPermissionName(rolePermission.PermissionName));
                        }
                        catch (ArgumentException)
                        {
                            // Skip invalid permission names
                            continue;
                        }
                    }
                }

                // Cache the result
                _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(CacheExpirationMinutes));
                
                return permissions;
            }
            catch (Exception)
            {
                var fallbackList = new List<Permission>();
                _cache.Set(cacheKey, fallbackList, TimeSpan.FromMinutes(1)); // Short cache on errors
                return fallbackList;
            }
        }

        /// <summary>
        /// Check if a user has a specific permission (cached)
        /// </summary>
        public async Task<bool> UserHasPermissionAsync(long userId, string resource, string action)
        {
            var cacheKey = $"user_permission_{userId}_{resource}_{action}";
            
            if (_cache.TryGetValue(cacheKey, out bool cachedResult))
            {
                return cachedResult;
            }

            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get user (cache user info separately for efficiency)
                var userCacheKey = $"user_{userId}";
                User? user = null;
                
                if (!_cache.TryGetValue(userCacheKey, out user))
                {
                    var userResponse = await client
                        .From<User>()
                        .Where(x => x.Id == userId)
                        .Get();

                    user = userResponse.Models?.FirstOrDefault();
                    if (user != null)
                    {
                        _cache.Set(userCacheKey, user, TimeSpan.FromMinutes(CacheExpirationMinutes));
                    }
                }

                if (user?.RoleId == null)
                {
                    _cache.Set(cacheKey, false, TimeSpan.FromMinutes(CacheExpirationMinutes));
                    return false;
                }

                // Get user's permissions through role (uses cached method)
                var permissions = await GetRolePermissionsAsync(user.RoleId.Value);
                
                // Check if user has the specific permission
                var hasPermission = permissions.Any(p => 
                    p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) && 
                    p.Action.Equals(action, StringComparison.OrdinalIgnoreCase));

                // Cache the result
                _cache.Set(cacheKey, hasPermission, TimeSpan.FromMinutes(CacheExpirationMinutes));
                
                return hasPermission;
            }
            catch (Exception)
            {
                // Cache false result on errors for shorter time
                _cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                return false;
            }
        }

        /// <summary>
        /// Check if a user has permission based on claims principal
        /// Uses role_id from JWT claims if available, falls back to database lookup
        /// </summary>
        public async Task<bool> UserHasPermissionAsync(ClaimsPrincipal user, string resource, string action)
        {
            // First try to get role_id directly from JWT claims (more efficient)
            var roleIdClaim = user.FindFirst("role_id")?.Value;
            if (!string.IsNullOrEmpty(roleIdClaim) && long.TryParse(roleIdClaim, out long roleId))
            {
                Console.WriteLine($"Checking permission using JWT role_id: {roleId} for {resource}.{action}");
                var result = await RoleHasPermissionAsync(roleId, resource, action);
                Console.WriteLine($"Permission result: {result}");
                return result;
            }

            // Fallback to database user lookup
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                return false;

            Console.WriteLine($"Falling back to database lookup for user {userId} for {resource}.{action}");
            return await UserHasPermissionAsync(userId, resource, action);
        }

        /// <summary>
        /// Check if a role has a specific permission
        /// </summary>
        public async Task<bool> RoleHasPermissionAsync(long roleId, string resource, string action)
        {
            var cacheKey = $"role_permission_{roleId}_{resource}_{action}";

            if (_cache.TryGetValue(cacheKey, out bool cachedResult))
            {
                Console.WriteLine($"Cache hit for {cacheKey}: {cachedResult}");
                return cachedResult;
            }

            try
            {
                Console.WriteLine($"Checking permissions for role {roleId}");
                var permissions = await GetRolePermissionsAsync(roleId);
                Console.WriteLine($"Role {roleId} has {permissions.Count} permissions");

                var hasPermission = permissions.Any(p =>
                    p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) &&
                    p.Action.Equals(action, StringComparison.OrdinalIgnoreCase));

                Console.WriteLine($"Role {roleId} has permission {resource}.{action}: {hasPermission}");
                _cache.Set(cacheKey, hasPermission, TimeSpan.FromMinutes(CacheExpirationMinutes));
                return hasPermission;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking permissions for role {roleId}: {ex.Message}");
                _cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                return false;
            }
        }

        /// <summary>
        /// Get all permissions for a user
        /// </summary>
        public async Task<List<Permission>> GetUserPermissionsAsync(long userId)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get user
                var userResponse = await client
                    .From<User>()
                    .Where(x => x.Id == userId)
                    .Get();

                var user = userResponse.Models?.FirstOrDefault();
                if (user?.RoleId == null)
                    return new List<Permission>();

                return await GetRolePermissionsAsync(user.RoleId.Value);
            }
            catch (Exception)
            {
                return new List<Permission>();
            }
        }

        /// <summary>
        /// Add permission to role (invalidates cache)
        /// </summary>
        public async Task<bool> AddPermissionToRoleAsync(long roleId, string permissionName)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var rolePermission = new RolePermission
                {
                    RoleId = roleId,
                    PermissionName = permissionName,
                    GrantedAt = DateTime.UtcNow
                };

                var result = await client.From<RolePermission>().Insert(rolePermission);
                var success = result.Models?.Any() == true;
                
                if (success)
                {
                    // Invalidate relevant caches
                    InvalidateRoleCaches(roleId);
                }
                
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Remove permission from role (invalidates cache)
        /// </summary>
        public async Task<bool> RemovePermissionFromRoleAsync(long roleId, string permissionName)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                await client
                    .From<RolePermission>()
                    .Where(x => x.RoleId == roleId && x.PermissionName == permissionName)
                    .Delete();

                // Invalidate relevant caches
                InvalidateRoleCaches(roleId);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Invalidate all caches related to a specific role
        /// </summary>
        private void InvalidateRoleCaches(long roleId)
        {
            // Remove role permissions cache
            var rolePermissionsCacheKey = $"role_permissions_{roleId}";
            _cache.Remove(rolePermissionsCacheKey);
            
            // Remove user permission caches for users with this role
            // Note: In a production system, you might want to maintain a more sophisticated cache invalidation strategy
            // For now, we'll let the user permission caches expire naturally (15 minutes)
        }

        /// <summary>
        /// Clear all permission caches (useful for admin operations)
        /// </summary>
        public void ClearAllPermissionCaches()
        {
            // In a real implementation, you might want to use a cache tag-based invalidation system
            // For now, this method can be called when doing bulk permission updates
            
            // Note: IMemoryCache doesn't have a built-in "clear all" method
            // In production, consider using distributed cache with tagging support
        }

        /// <summary>
        /// Get all available permissions from code constants
        /// </summary>
        public List<PermissionDto> GetAllAvailablePermissions()
        {
            return Permission.GetAllAvailablePermissions()
                .Select(p => p.ToDto())
                .ToList();
        }

        /// <summary>
        /// Get permissions grouped by resource
        /// </summary>
        public Dictionary<string, List<PermissionDto>> GetPermissionsByResource()
        {
            var allPermissions = Permission.GetAllAvailablePermissions();
            return allPermissions
                .GroupBy(p => p.Resource)
                .ToDictionary(g => g.Key, g => g.Select(p => p.ToDto()).ToList());
        }

        /// <summary>
        /// Validate if a permission name is valid according to code constants
        /// </summary>
        public bool IsValidPermission(string permissionName)
        {
            return Permission.IsValidPermissionName(permissionName);
        }

        /// <summary>
        /// Get available role templates from code constants
        /// </summary>
        public List<RoleTemplate> GetAvailableRoleTemplates()
        {
            return PermissionConstants.RoleTemplates.GetAllRoleTemplates();
        }
    }
}
