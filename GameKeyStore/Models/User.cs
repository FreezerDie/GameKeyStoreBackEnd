using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace GameKeyStore.Models
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("role_id")]
        public long? RoleId { get; set; }

        [Column("is_staff")]
        public bool? IsStaff { get; set; }

        // Convert to DTO for API responses
        public UserDto ToDto()
        {
            return new UserDto
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                Email = this.Email,
                Name = this.Name,
                Username = this.Username,
                RoleId = this.RoleId,
                IsStaff = this.IsStaff
            };
        }
    }

}
