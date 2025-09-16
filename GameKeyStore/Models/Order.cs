using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    // Database model - used for Supabase operations
    [Table("order")]
    public class Order : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("total_price")]
        public long TotalPrice { get; set; }

        [Column("user_id")]
        public long? UserId { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }

        // Convert to DTO for API responses
        public OrderDto ToDto()
        {
            return new OrderDto
            {
                Id = this.Id,
                TotalPrice = this.TotalPrice,
                UserId = this.UserId,
                Status = this.Status,
                Comment = this.Comment
            };
        }
    }

    // DTO model - used for API responses (no BaseModel inheritance = no serialization issues)
    public class OrderDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("total_price")]
        public long TotalPrice { get; set; }

        [JsonPropertyName("user_id")]
        public long? UserId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    // Extended DTO with sub-orders information
    public class OrderWithSubOrdersDto : OrderDto
    {
        [JsonPropertyName("sub_orders")]
        public List<SubOrderWithDetailsDto>? SubOrders { get; set; }
    }

    // Request DTO for creating orders
    public class CreateOrderDto
    {
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    // Response DTO for checkout operations
    public class CheckoutResponseDto
    {
        [JsonPropertyName("order")]
        public OrderWithSubOrdersDto? Order { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
