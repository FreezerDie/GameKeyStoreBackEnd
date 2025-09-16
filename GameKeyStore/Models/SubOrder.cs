using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("sub_orders")]
    public class SubOrder : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("order_id")]
        public long? OrderId { get; set; }

        [Column("user_id")]
        public long? UserId { get; set; }

        [Column("game_id")]
        public long? GameId { get; set; }

        [Column("game_key_id")]
        public long? GameKeyId { get; set; }

        // Convert to DTO for API responses
        public SubOrderDto ToDto()
        {
            return new SubOrderDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                OrderId = this.OrderId,
                UserId = this.UserId,
                GameId = this.GameId,
                GameKeyId = this.GameKeyId
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class SubOrderDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("order_id")]
        public long? OrderId { get; set; }

        [JsonPropertyName("user_id")]
        public long? UserId { get; set; }

        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }

        [JsonPropertyName("game_key_id")]
        public long? GameKeyId { get; set; }
    }

    // Extended DTO with game and game key information
    public class SubOrderWithDetailsDto : SubOrderDto
    {
        [JsonPropertyName("game")]
        public GameDto? Game { get; set; }

        [JsonPropertyName("game_key")]
        public GameKeyDto? GameKey { get; set; }

        [JsonPropertyName("price")]
        public float? Price { get; set; }
    }
}
