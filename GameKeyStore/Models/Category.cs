using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("categories")]
    public class Category : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("cover")]
        public string? Cover { get; set; }

        [Column("is_active")]
        public bool? IsActive { get; set; }

        // Convert to DTO for API responses
        public CategoryDto ToDto()
        {
            return new CategoryDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                Name = this.Name,
                Description = this.Description,
                Cover = this.Cover,
                IsActive = this.IsActive
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class CategoryDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("cover")]
        public string? Cover { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
    }
}
