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
        // JWT Claim name constants
        public const string ClaimNameId = "id";
        public const string ClaimNameEmail = "email";
        public const string ClaimNameName = "name";
        public const string ClaimNameUsername = "username";
        public const string ClaimNameIsStaff = "is_staff";
        public const string ClaimNameRoleId = "role_id";

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

                // Get the default "User" role for new registrations
                long? defaultRoleId = await GetDefaultUserRoleIdAsync();
                
                // Create new user with default role
                var newUser = new User
                {
                    Email = registerDto.Email,
                    Name = registerDto.Name,
                    Username = registerDto.Username,
                    Password = hashedPassword,
                    RoleId = defaultRoleId, // Assign default User role
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

        private Task<string> GenerateJwtTokenAsync(User user)
        {
            var key = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "your-super-secret-jwt-key-that-should-be-changed-in-production";
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimNameId, user.Id.ToString()),
                new Claim(ClaimNameEmail, user.Email),
                new Claim(ClaimNameName, user.Name),
                new Claim(ClaimNameUsername, user.Username),
                new Claim(ClaimNameIsStaff, (user.IsStaff ?? false).ToString().ToLower()),
                new Claim(ClaimNameRoleId, user.RoleId?.ToString() ?? ""),
            };


            var token = new JwtSecurityToken(
                issuer: "GameKeyStore",
                audience: "GameKeyStore",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
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

        public async Task<User?> UpdateUserProfileAsync(long userId, UpdateProfileDto updateDto)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get current user
                var currentUser = await GetUserByIdAsync(userId);
                if (currentUser == null)
                {
                    return null;
                }

                // Check if email is being changed and if it's already taken
                if (!string.IsNullOrWhiteSpace(updateDto.Email) && updateDto.Email != currentUser.Email)
                {
                    var existingUserByEmail = await client.From<User>()
                        .Where(u => u.Email == updateDto.Email)
                        .Get();

                    if (existingUserByEmail.Models.Any())
                    {
                        throw new InvalidOperationException("Email is already taken by another user");
                    }
                }

                // Check if username is being changed and if it's already taken
                if (!string.IsNullOrWhiteSpace(updateDto.Username) && updateDto.Username != currentUser.Username)
                {
                    var existingUserByUsername = await client.From<User>()
                        .Where(u => u.Username == updateDto.Username)
                        .Get();

                    if (existingUserByUsername.Models.Any())
                    {
                        throw new InvalidOperationException("Username is already taken by another user");
                    }
                }

                // Update user fields
                var updatedUser = new User
                {
                    Id = currentUser.Id,
                    Email = !string.IsNullOrWhiteSpace(updateDto.Email) ? updateDto.Email : currentUser.Email,
                    Name = !string.IsNullOrWhiteSpace(updateDto.Name) ? updateDto.Name : currentUser.Name,
                    Username = !string.IsNullOrWhiteSpace(updateDto.Username) ? updateDto.Username : currentUser.Username,
                    Password = currentUser.Password, // Keep existing password
                    RoleId = currentUser.RoleId,
                    IsStaff = currentUser.IsStaff,
                    CreatedAt = currentUser.CreatedAt
                };

                var result = await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Update(updatedUser);

                return result.Models.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> ChangeUserPasswordAsync(long userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get current user
                var currentUser = await GetUserByIdAsync(userId);
                if (currentUser == null)
                {
                    return false;
                }

                // Verify current password
                if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, currentUser.Password))
                {
                    throw new UnauthorizedAccessException("Current password is incorrect");
                }

                // Hash new password
                var hashedNewPassword = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);

                // Update password
                var updatedUser = new User
                {
                    Id = currentUser.Id,
                    Email = currentUser.Email,
                    Name = currentUser.Name,
                    Username = currentUser.Username,
                    Password = hashedNewPassword,
                    RoleId = currentUser.RoleId,
                    IsStaff = currentUser.IsStaff,
                    CreatedAt = currentUser.CreatedAt
                };

                var result = await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Update(updatedUser);

                return result.Models.Any();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserAccountAsync(long userId)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Verify user exists
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Delete the user account
                await client.From<User>()
                    .Where(u => u.Id == userId)
                    .Delete();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Get the default "User" role ID for new registrations
        /// </summary>
        private async Task<long?> GetDefaultUserRoleIdAsync()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // First try direct match with lowercase "user"
                var roleResponse = await client
                    .From<Role>()
                    .Where(r => r.Name == "user")
                    .Get();

                var userRole = roleResponse.Models?.FirstOrDefault();
                
                if (userRole != null)
                {
                    return userRole.Id;
                }

                // Fallback: Get all roles and find case-insensitive match
                var allRolesResponse = await client
                    .From<Role>()
                    .Get();

                var allRoles = allRolesResponse.Models ?? new List<Role>();
                userRole = allRoles.FirstOrDefault(r => 
                    string.Equals(r.Name, "user", StringComparison.OrdinalIgnoreCase));

                return userRole?.Id;
            }
            catch (Exception)
            {
                // If we can't get the default role, return null (user will have no role)
                return null;
            }
        }
    }
}
