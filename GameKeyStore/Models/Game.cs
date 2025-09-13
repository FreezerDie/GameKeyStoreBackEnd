using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("games")]
    public class Game : BaseModel
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

        [Column("category_id")]
        public long? CategoryId { get; set; }

        // Convert to DTO for API responses
        public GameDto ToDto()
        {
            return new GameDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                Name = this.Name,
                Description = this.Description,
                Cover = this.Cover,
                CategoryId = this.CategoryId
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class GameDto
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

        [JsonPropertyName("category_id")]
        public long? CategoryId { get; set; }
    }

    // Extended DTO with category information
    public class GameWithCategoryDto : GameDto
    {
        [JsonPropertyName("category")]
        public CategoryDto? Category { get; set; }
    }
}
