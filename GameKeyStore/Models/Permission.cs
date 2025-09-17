using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using GameKeyStore.Constants;

namespace GameKeyStore.Models
{
    // Permission model - works with code-based permission system
    // Permissions are defined in PermissionConstants, not stored in database
    public class Permission
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;

        // Convert to DTO for API responses
        public PermissionDto ToDto()
        {
            return new PermissionDto
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                Resource = this.Resource,
                Action = this.Action
            };
        }

        /// <summary>
        /// Create Permission from PermissionDefinition
        /// </summary>
        public static Permission FromDefinition(PermissionDefinition definition, long? id = null)
        {
            return new Permission
            {
                Id = id ?? 0,
                Name = definition.Name,
                Description = definition.Description,
                Resource = definition.Resource,
                Action = definition.Action
            };
        }

        /// <summary>
        /// Create Permission from permission name (e.g., "games.read")
        /// </summary>
        public static Permission FromPermissionName(string permissionName)
        {
            // Try to find the permission definition first
            var allPermissions = PermissionConstants.GetAllPermissions();
            var definition = allPermissions.FirstOrDefault(p => p.Name == permissionName);
            
            if (definition != null)
            {
                return FromDefinition(definition);
            }

            // If not found in constants, parse the name
            var parts = permissionName.Split('.');
            if (parts.Length == 2)
            {
                return new Permission
                {
                    Id = 0,
                    Name = permissionName,
                    Resource = parts[0],
                    Action = parts[1],
                    Description = $"Permission for {parts[1]} action on {parts[0]} resource"
                };
            }

            throw new ArgumentException($"Invalid permission name format: {permissionName}");
        }

        /// <summary>
        /// Get all available permissions from code constants
        /// </summary>
        public static List<Permission> GetAllAvailablePermissions()
        {
            return PermissionConstants.GetAllPermissions()
                .Select((def, index) => FromDefinition(def, index + 1))
                .ToList();
        }

        /// <summary>
        /// Check if a permission name is valid
        /// </summary>
        public static bool IsValidPermissionName(string permissionName)
        {
            var allPermissions = PermissionConstants.GetAllPermissions();
            return allPermissions.Any(p => p.Name == permissionName);
        }
    }

    // Database model for role-permission relationship
    [Table("role_permissions")]
    public class RolePermission : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("role_id")]
        public long RoleId { get; set; }

        [Column("permission")]  // Updated to match corrected database column name
        public string PermissionName { get; set; } = string.Empty;

        [Column("granted_at")]
        public DateTime GrantedAt { get; set; }
    }

    // DTO model - used for API responses
    public class PermissionDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("resource")]
        public string Resource { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;
    }

    // Extended Role DTO with permissions
    public class RoleWithPermissionsDto : RoleDto
    {
        [JsonPropertyName("permissions")]
        public List<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();
    }
}
