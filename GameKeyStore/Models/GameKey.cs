using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("game_keys")]
    public class GameKey : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("key")]
        public string? Key { get; set; }

        [Column("game_id")]
        public long? GameId { get; set; }

        // Convert to DTO for API responses
        public GameKeyDto ToDto()
        {
            return new GameKeyDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                Key = this.Key,
                GameId = this.GameId
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class GameKeyDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }
    }

    // Extended DTO with game information
    public class GameKeyWithGameDto : GameKeyDto
    {
        [JsonPropertyName("game")]
        public GameDto? Game { get; set; }
    }
}
