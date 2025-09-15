using GameKeyStore.Constants;
using GameKeyStore.Models;

namespace GameKeyStore.Services
{
    /// <summary>
    /// Service for managing permissions and roles with predefined constants
    /// </summary>
    public class PermissionManager
    {
        private readonly SupabaseService _supabaseService;
        private readonly PermissionService _permissionService;
        private readonly ILogger<PermissionManager> _logger;

        public PermissionManager(SupabaseService supabaseService, PermissionService permissionService, ILogger<PermissionManager> logger)
        {
            _supabaseService = supabaseService;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// Validate that all permissions are available in code constants
        /// (No database seeding needed since permissions are code-based)
        /// </summary>
        public Task<bool> ValidatePermissionsAsync()
        {
            try
            {
                // Get all permission definitions from code
                var allPermissions = PermissionConstants.GetAllPermissions();
                
                _logger.LogInformation("Validated {Count} permissions from code constants", allPermissions.Count);
                
                return Task.FromResult(allPermissions.Any());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating permissions");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Create a role from a template
        /// </summary>
        public async Task<Role?> CreateRoleFromTemplateAsync(RoleTemplate template)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Create the role
                var newRole = new Role
                {
                    Name = template.Name,
                    Description = template.Description
                };

                var roleResult = await client
                    .From<Role>()
                    .Insert(newRole);

                var createdRole = roleResult.Models?.FirstOrDefault();
                if (createdRole == null)
                {
                    _logger.LogError("Failed to create role: {RoleName}", template.Name);
                    return null;
                }

                // Assign permissions to the role
                await AssignPermissionsToRoleAsync(createdRole.Id, template.Permissions);

                _logger.LogInformation("Created role '{RoleName}' with {PermissionCount} permissions", 
                    template.Name, template.Permissions.Length);
                
                return createdRole;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role from template: {RoleName}", template.Name);
                return null;
            }
        }

        /// <summary>
        /// Assign multiple permissions to a role
        /// </summary>
        public async Task<bool> AssignPermissionsToRoleAsync(long roleId, PermissionDefinition[] permissionDefinitions)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get existing role permissions to avoid duplicates
                var existingRolePermissions = await _permissionService.GetRolePermissionsAsync(roleId);
                var existingPermissionNames = existingRolePermissions.Select(p => p.Name).ToHashSet();

                // Create role-permission relationships (only storing permission names)
                var rolePermissions = new List<RolePermission>();
                
                foreach (var permDef in permissionDefinitions)
                {
                    if (!existingPermissionNames.Contains(permDef.Name))
                    {
                        rolePermissions.Add(new RolePermission
                        {
                            RoleId = roleId,
                            PermissionName = permDef.Name,
                            GrantedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        _logger.LogDebug("Permission {PermissionName} already exists for role {RoleId}", permDef.Name, roleId);
                    }
                }

                if (rolePermissions.Any())
                {
                    var result = await client
                        .From<RolePermission>()
                        .Insert(rolePermissions);

                    var assignedCount = result.Models?.Count ?? 0;
                    _logger.LogInformation("Assigned {Count} permissions to role {RoleId}", assignedCount, roleId);
                    
                    // Clear caches
                    _permissionService.ClearAllPermissionCaches();
                    
                    return assignedCount > 0;
                }

                return true; // All permissions already assigned
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning permissions to role {RoleId}", roleId);
                return false;
            }
        }

        /// <summary>
        /// Seed all default roles using templates
        /// </summary>
        public async Task<bool> SeedRolesAsync()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get existing roles to avoid duplicates
                var existingRolesResponse = await client
                    .From<Role>()
                    .Get();

                var existingRoles = existingRolesResponse.Models ?? new List<Role>();
                var existingRoleNames = existingRoles.Select(r => r.Name).ToHashSet();

                // Get all role templates
                var roleTemplates = PermissionConstants.RoleTemplates.GetAllRoleTemplates();
                var successCount = 0;

                foreach (var template in roleTemplates)
                {
                    if (!existingRoleNames.Contains(template.Name))
                    {
                        var createdRole = await CreateRoleFromTemplateAsync(template);
                        if (createdRole != null)
                        {
                            successCount++;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Role '{RoleName}' already exists", template.Name);
                    }
                }

                _logger.LogInformation("Seeded {Count} new roles", successCount);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding roles");
                return false;
            }
        }

        /// <summary>
        /// Initialize the permission system (validate permissions and seed roles)
        /// </summary>
        public async Task<bool> InitializePermissionSystemAsync()
        {
            try
            {
                _logger.LogInformation("Initializing permission system...");

                // First, validate all permissions are available in code
                var permissionsValid = await ValidatePermissionsAsync();
                if (!permissionsValid)
                {
                    _logger.LogError("Failed to validate permissions");
                    return false;
                }

                // Then, seed all roles with their permissions
                var rolesSeeded = await SeedRolesAsync();
                if (!rolesSeeded)
                {
                    _logger.LogError("Failed to seed roles");
                    return false;
                }

                _logger.LogInformation("Permission system initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing permission system");
                return false;
            }
        }

        /// <summary>
        /// Get all available permissions grouped by resource
        /// </summary>
        public Dictionary<string, List<PermissionDefinition>> GetAvailablePermissions()
        {
            return PermissionConstants.GetPermissionsByResource();
        }

        /// <summary>
        /// Get all available role templates
        /// </summary>
        public List<RoleTemplate> GetRoleTemplates()
        {
            return PermissionConstants.RoleTemplates.GetAllRoleTemplates();
        }

        /// <summary>
        /// Check if a permission exists by name
        /// </summary>
        public bool HasPermission(string permissionName)
        {
            return PermissionConstants.GetAllPermissions()
                .Any(p => p.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get permission definition by name
        /// </summary>
        public PermissionDefinition? GetPermissionDefinition(string permissionName)
        {
            return PermissionConstants.GetAllPermissions()
                .FirstOrDefault(p => p.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validate that all permissions in a list exist
        /// </summary>
        public (bool IsValid, string[] MissingPermissions) ValidatePermissions(string[] permissionNames)
        {
            var allPermissions = PermissionConstants.GetAllPermissions().Select(p => p.Name).ToHashSet();
            var missingPermissions = permissionNames.Where(name => !allPermissions.Contains(name)).ToArray();
            
            return (missingPermissions.Length == 0, missingPermissions);
        }

        /// <summary>
        /// Create a custom role with specific permissions
        /// </summary>
        public async Task<Role?> CreateCustomRoleAsync(string roleName, string roleDescription, string[] permissionNames)
        {
            try
            {
                // Validate permissions first
                var validation = ValidatePermissions(permissionNames);
                if (!validation.IsValid)
                {
                    _logger.LogError("Invalid permissions provided for role {RoleName}: {MissingPermissions}", 
                        roleName, string.Join(", ", validation.MissingPermissions));
                    return null;
                }

                // Get permission definitions (filter out nulls properly)
                var permissions = permissionNames
                    .Select(GetPermissionDefinition)
                    .Where(p => p != null)
                    .Cast<PermissionDefinition>()
                    .ToArray();

                // Create custom template
                var customTemplate = new RoleTemplate(roleName, roleDescription, permissions);
                
                return await CreateRoleFromTemplateAsync(customTemplate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating custom role: {RoleName}", roleName);
                return null;
            }
        }

        /// <summary>
        /// Update role permissions (replace all permissions)
        /// </summary>
        public async Task<bool> UpdateRolePermissionsAsync(long roleId, string[] permissionNames)
        {
            try
            {
                // Validate permissions
                var validation = ValidatePermissions(permissionNames);
                if (!validation.IsValid)
                {
                    _logger.LogError("Invalid permissions provided for role {RoleId}: {MissingPermissions}", 
                        roleId, string.Join(", ", validation.MissingPermissions));
                    return false;
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Remove all existing permissions for this role
                await client
                    .From<RolePermission>()
                    .Where(x => x.RoleId == roleId)
                    .Delete();

                // Add new permissions (filter out nulls properly)
                var permissions = permissionNames
                    .Select(GetPermissionDefinition)
                    .Where(p => p != null)
                    .Cast<PermissionDefinition>()
                    .ToArray();

                var success = await AssignPermissionsToRoleAsync(roleId, permissions);
                
                if (success)
                {
                    _logger.LogInformation("Updated permissions for role {RoleId}", roleId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role permissions for role {RoleId}", roleId);
                return false;
            }
        }
    }
}
