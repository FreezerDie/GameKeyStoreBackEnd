using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("roles")]
    public class Role : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        // Convert to DTO for API responses
        public RoleDto ToDto()
        {
            return new RoleDto
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class RoleDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
