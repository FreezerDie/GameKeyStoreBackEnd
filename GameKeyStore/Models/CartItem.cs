using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("cart_items")]
    public class CartItem : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("game_id")]
        public long? GameId { get; set; }

        [Column("game_key_id")]
        public long? GameKeyId { get; set; }

        [Column("user_id")]
        public long? UserId { get; set; }

        // Convert to DTO for API responses
        public CartItemDto ToDto()
        {
            return new CartItemDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                GameId = this.GameId,
                GameKeyId = this.GameKeyId,
                UserId = this.UserId
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class CartItemDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }

        [JsonPropertyName("game_key_id")]
        public long? GameKeyId { get; set; }

        [JsonPropertyName("user_id")]
        public long? UserId { get; set; }
    }

    // Extended DTO with game and game key information
    public class CartItemWithDetailsDto : CartItemDto
    {
        [JsonPropertyName("game")]
        public GameDto? Game { get; set; }

        [JsonPropertyName("game_key")]
        public GameKeyDto? GameKey { get; set; }
    }

    // Request DTO for adding items to cart
    public class AddToCartDto
    {
        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }

        [JsonPropertyName("game_key_id")]
        public long? GameKeyId { get; set; }
    }

    // Request DTO for updating cart items
    public class UpdateCartItemDto
    {
        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }

        [JsonPropertyName("game_key_id")]
        public long? GameKeyId { get; set; }
    }
}
