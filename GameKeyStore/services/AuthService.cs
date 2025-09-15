using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using GameKeyStore.Models;
using BCrypt.Net;

namespace GameKeyStore.Services
{
    public class AuthService
    {
        private readonly SupabaseService _supabaseService;
        private readonly IConfiguration _configuration;

        public AuthService(SupabaseService supabaseService, IConfiguration configuration)
        {
            _supabaseService = supabaseService;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if user already exists by email or username
                var existingUserByEmail = await client.From<User>()
                    .Where(u => u.Email == registerDto.Email)
                    .Get();

                if (existingUserByEmail.Models.Count > 0)
                {
                    return null; // User with this email already exists
                }

                var existingUserByUsername = await client.From<User>()
                    .Where(u => u.Username == registerDto.Username)
                    .Get();

                if (existingUserByUsername.Models.Count > 0)
                {
                    return null; // User with this username already exists
                }

                // Hash password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

                // Create new user (role_id can be set later or default to null for basic user)
                var newUser = new User
                {
                    Email = registerDto.Email,
                    Name = registerDto.Name,
                    Username = registerDto.Username,
                    Password = hashedPassword,
                    RoleId = null, // Can be set to default role ID if needed
                    CreatedAt = DateTime.UtcNow
                };

                var result = await client.From<User>().Insert(newUser);
                var createdUser = result.Models.FirstOrDefault();

                if (createdUser == null)
                {
                    return null;
                }

                return await GenerateAuthResponseAsync(createdUser);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                await _supabaseService.InitializeAsync();                                   
                var client = _supabaseService.GetClient();

                // Find user by email or username
                var userByEmail = await client.From<User>()
                    .Where(u => u.Email == loginDto.EmailField)
                    .Get();

                var user = userByEmail.Models.FirstOrDefault();

                if (user == null)
                {
                    var userByUsername = await client.From<User>()
                        .Where(u => u.Username == loginDto.EmailField)
                        .Get();

                    user = userByUsername.Models.FirstOrDefault();
                }

                if (user == null)
                {
                    return null; // User not found
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(loginDto.PasswordField, user.Password))
                {
                    return null; // Invalid password
                }

                return await GenerateAuthResponseAsync(user);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(User user)
        {
            var token = await GenerateJwtTokenAsync(user);
            var refreshToken = GenerateRefreshToken();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Token expires in 7 days
                User = user.ToDto()
            };
        }

        private async Task<string> GenerateJwtTokenAsync(User user)
        {
            var key = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "your-super-secret-jwt-key-that-should-be-changed-in-production";
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("username", user.Username),
                new Claim("is_staff", (user.IsStaff ?? false).ToString().ToLower()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add role claim if user has a role (but NO permissions in JWT)
            if (user.RoleId.HasValue)
            {
                try
                {
                    // Get user's role name for the token
                    await _supabaseService.InitializeAsync();
                    var client = _supabaseService.GetClient();
                    
                    var roleResponse = await client
                        .From<Role>()
                        .Where(x => x.Id == user.RoleId.Value)
                        .Get();

                    var role = roleResponse.Models?.FirstOrDefault();
                    if (role != null)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role.Name));
                        claims.Add(new Claim("role_id", user.RoleId.Value.ToString()));
                    }
                }
                catch (Exception)
                {
                    // If role lookup fails, continue without role claims
                }
            }

            var token = new JwtSecurityToken(
                issuer: "GameKeyStore",
                audience: "GameKeyStore",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }

        public async Task<User?> GetUserByIdAsync(long userId)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var result = await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Get();

                return result.Models.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
