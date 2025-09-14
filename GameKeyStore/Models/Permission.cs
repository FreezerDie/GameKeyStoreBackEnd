using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("permissions")]
    public class Permission : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("resource")]
        public string Resource { get; set; } = string.Empty;

        [Column("action")]
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
    }

    // Database model for role-permission relationship
    [Table("role_permissions")]
    public class RolePermission : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("role_id")]
        public long RoleId { get; set; }

        [Column("premission")]  // Note: matches the database column name (with typo)
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
