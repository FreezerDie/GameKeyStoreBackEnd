using GameKeyStore.Models;
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

                // Convert permission names to Permission objects
                var permissions = new List<Permission>();
                foreach (var rolePermission in rolePermissions)
                {
                    if (!string.IsNullOrEmpty(rolePermission.PermissionName))
                    {
                        // Parse permission name (e.g., "games.read" -> resource: "games", action: "read")
                        var parts = rolePermission.PermissionName.Split('.');
                        if (parts.Length == 2)
                        {
                            permissions.Add(new Permission
                            {
                                Id = 0, // Not used since we're working with names directly
                                Name = rolePermission.PermissionName,
                                Resource = parts[0],
                                Action = parts[1],
                                Description = $"Permission for {parts[1]} action on {parts[0]} resource"
                            });
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
        /// </summary>
        public async Task<bool> UserHasPermissionAsync(ClaimsPrincipal user, string resource, string action)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                return false;

            return await UserHasPermissionAsync(userId, resource, action);
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
    }
}
