using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Models;
using GameKeyStore.Services;
using GameKeyStore.Authorization;
using System.Security.Claims;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, SupabaseService supabaseService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _supabaseService = supabaseService;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.RegisterAsync(registerDto);

                if (result == null)
                {
                    return BadRequest(new { message = "User with this email or username already exists" });
                }

                _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new { message = "Internal server error during registration" });
            }
        }

        /// <summary>
        /// Login user
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.LoginAsync(loginDto);

                if (result == null)
                {
                    return Unauthorized(new { message = "Invalid email/username or password" });
                }

                _logger.LogInformation("User logged in successfully: {Email}", loginDto.EmailField);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(500, new { message = "Internal server error during login" });
            }
        }

        /// <summary>
        /// Get current user profile (requires authentication)
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _authService.GetUserByIdAsync(userId);
                
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(user.ToDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Test endpoint to verify JWT authentication is working
        /// </summary>
        [HttpGet("protected")]
        [Authorize]
        public IActionResult ProtectedEndpoint()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var username = User.FindFirst("username")?.Value;
            var isStaff = User.FindFirst("is_staff")?.Value;
            
            return Ok(new 
            { 
                message = "This is a protected endpoint",
                userId = userId,
                email = email,
                username = username,
                isStaff = isStaff,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Debug endpoint to check user permissions
        /// </summary>
        [HttpGet("debug-permissions")]
        [Authorize]
        public async Task<IActionResult> DebugPermissions()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _authService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Get user permissions through PermissionService
                var permissionService = HttpContext.RequestServices.GetRequiredService<PermissionService>();
                var userPermissions = await permissionService.GetUserPermissionsAsync(userId);

                // Check specific games.read permission
                var hasGamesRead = await permissionService.UserHasPermissionAsync(userId, "games", "read");

                // Get role permissions from database directly
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                var rolePermissionsResponse = await client
                    .From<RolePermission>()
                    .Where(x => x.RoleId == user.RoleId)
                    .Get();

                var rolePermissions = rolePermissionsResponse.Models ?? new List<RolePermission>();

                return Ok(new 
                { 
                    message = "Permission debug information",
                    user = new {
                        id = user.Id,
                        email = user.Email,
                        username = user.Username,
                        roleId = user.RoleId,
                        isStaff = user.IsStaff
                    },
                    permissions = new {
                        userPermissions = userPermissions.Select(p => new {
                            name = p.Name,
                            resource = p.Resource,
                            action = p.Action,
                            description = p.Description
                        }).ToList(),
                        hasGamesRead = hasGamesRead,
                        rolePermissionsFromDB = rolePermissions.Select(rp => new {
                            id = rp.Id,
                            roleId = rp.RoleId,
                            permissionName = rp.PermissionName,
                            grantedAt = rp.GrantedAt
                        }).ToList()
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Debug error",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Get user profile with role information
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="includeRole">Whether to include role information</param>
        /// <returns>User profile</returns>
        [HttpGet("user/{id}")]
        [RequireUsersRead]
        public async Task<IActionResult> GetUser(long id, [FromQuery] bool includeRole = false)
        {
            try
            {
                var user = await _authService.GetUserByIdAsync(id);
                
                if (user == null)
                {
                    return NotFound(new { 
                        message = $"User with ID {id} not found" 
                    });
                }

                if (includeRole && user.RoleId.HasValue)
                {
                    await _supabaseService.InitializeAsync();
                    var client = _supabaseService.GetClient();
                    
                    // Fetch role information
                    var roleResponse = await client
                        .From<Role>()
                        .Where(x => x.Id == user.RoleId.Value)
                        .Get();
                    
                    var role = roleResponse.Models?.FirstOrDefault();
                    
                    var userWithRole = new UserWithRoleDto
                    {
                        Id = user.Id,
                        CreatedAt = user.CreatedAt,
                        Email = user.Email,
                        Name = user.Name,
                        Username = user.Username,
                        RoleId = user.RoleId,
                        Role = role?.ToDto()
                    };
                    
                    return Ok(new { 
                        message = "User with role information found", 
                        data = userWithRole
                    });
                }
                else
                {
                    return Ok(new { 
                        message = "User found", 
                        data = user.ToDto()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get all users with optional role information (Admin endpoint)
        /// </summary>
        /// <param name="includeRole">Whether to include role information</param>
        /// <param name="roleId">Optional filter by role ID</param>
        /// <returns>List of users</returns>
        [HttpGet("users")]
        [RequireUsersAdmin]
        public async Task<IActionResult> GetUsers([FromQuery] bool includeRole = false, [FromQuery] long? roleId = null)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Execute query with optional role filter
                var usersResponse = roleId.HasValue
                    ? await client
                        .From<User>()
                        .Where(x => x.RoleId == roleId.Value)
                        .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get()
                    : await client
                        .From<User>()
                        .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get();
                
                var users = usersResponse.Models ?? new List<User>();
                
                if (includeRole && users.Any())
                {
                    // Get all unique role IDs from the users
                    var roleIds = users
                        .Where(u => u.RoleId.HasValue)
                        .Select(u => u.RoleId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Fetch roles in batch
                    var rolesResponse = await client
                        .From<Role>()
                        .Get();
                    
                    var allRoles = rolesResponse.Models ?? new List<Role>();
                    var roles = allRoles.Where(role => roleIds.Contains(role.Id)).ToDictionary(role => role.Id);
                    
                    // Create extended DTOs with role information
                    var usersWithRole = users.Select(user => 
                    {
                        var userWithRole = new UserWithRoleDto
                        {
                            Id = user.Id,
                            CreatedAt = user.CreatedAt,
                            Email = user.Email,
                            Name = user.Name,
                            Username = user.Username,
                            RoleId = user.RoleId,
                            Role = user.RoleId.HasValue && roles.ContainsKey(user.RoleId.Value)
                                ? roles[user.RoleId.Value].ToDto()
                                : null
                        };
                        return userWithRole;
                    }).ToList();
                    
                    return Ok(new { 
                        message = roleId.HasValue 
                            ? $"Users filtered by role {roleId} with role information fetched from database" 
                            : "Users with role information fetched from database",
                        count = usersWithRole.Count,
                        roleFilter = roleId,
                        data = usersWithRole
                    });
                }
                else
                {
                    // Convert to simple DTOs
                    var userDtos = users.Select(x => x.ToDto()).ToList();
                    
                    return Ok(new { 
                        message = roleId.HasValue 
                            ? $"Users filtered by role {roleId} fetched from database" 
                            : "Users fetched from database",
                        count = userDtos.Count,
                        roleFilter = roleId,
                        data = userDtos
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users");
                
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }
    }
}