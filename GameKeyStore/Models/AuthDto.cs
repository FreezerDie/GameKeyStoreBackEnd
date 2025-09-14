using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GameKeyStore.Models
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MinLength(3)]
        public string Username { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required]
        [JsonPropertyName("email")]
        public string EmailField { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("password")]
        public string PasswordField { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserDto User { get; set; } = new UserDto();
        public DateTime ExpiresAt { get; set; }
    }

    public class UserDto
    {
        public long Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        [JsonPropertyName("role_id")]
        public long? RoleId { get; set; }
        
        [JsonPropertyName("is_staff")]
        public bool? IsStaff { get; set; }
    }

    // Extended DTO with role information
    public class UserWithRoleDto : UserDto
    {
        [JsonPropertyName("role")]
        public RoleDto? Role { get; set; }
    }
}
