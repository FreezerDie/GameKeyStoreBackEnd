using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Constants;
using GameKeyStore.Authorization;
using GameKeyStore.Models;
using System.Text.Json.Serialization;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly PermissionManager _permissionManager;
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(PermissionManager permissionManager, SupabaseService supabaseService, ILogger<AdminController> logger)
        {
            _permissionManager = permissionManager;
            _supabaseService = supabaseService;
            _logger = logger;
        }

        /// <summary>
        /// Initialize the permission system (seed permissions and roles)
        /// </summary>
        [HttpPost("initialize-permissions")]
        [RequireRolesAdmin]
        public async Task<IActionResult> InitializePermissions()
        {
            try
            {
                var success = await _permissionManager.InitializePermissionSystemAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Permission system initialized successfully"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to initialize permission system"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing permission system");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Validate all available permissions (permissions are defined in code constants)
        /// </summary>
        [HttpPost("validate-permissions-system")]
        [RequireRolesAdmin]
        public async Task<IActionResult> ValidatePermissionsSystem()
        {
            try
            {
                var success = await _permissionManager.ValidatePermissionsAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Permissions validated successfully - all permissions are available from code constants"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to validate permissions"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating permissions");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Seed all default roles with their permissions
        /// </summary>
        [HttpPost("seed-roles")]
        [RequireRolesAdmin]
        public async Task<IActionResult> SeedRoles()
        {
            try
            {
                var success = await _permissionManager.SeedRolesAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Roles seeded successfully"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to seed roles"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding roles");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available permission definitions (from constants)
        /// </summary>
        [HttpGet("available-permissions")]
        [RequireRolesRead]
        public IActionResult GetAvailablePermissions()
        {
            try
            {
                var permissions = _permissionManager.GetAvailablePermissions();
                
                return Ok(new { 
                    message = "Available permissions retrieved from constants",
                    data = permissions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available permissions");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available role templates
        /// </summary>
        [HttpGet("role-templates")]
        [RequireRolesRead]
        public IActionResult GetRoleTemplates()
        {
            try
            {
                var templates = _permissionManager.GetRoleTemplates();
                
                var templatesData = templates.Select(t => new 
                {
                    name = t.Name,
                    permissions = t.Permissions.Select(p => new 
                    {
                        name = p.Name,
                        description = p.Description,
                        resource = p.Resource,
                        action = p.Action
                    }).ToArray()
                }).ToArray();

                return Ok(new { 
                    message = "Role templates retrieved",
                    count = templates.Count,
                    data = templatesData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role templates");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create a role from a template
        /// </summary>
        [HttpPost("create-role-from-template")]
        [RequireRolesWrite]
        public async Task<IActionResult> CreateRoleFromTemplate([FromBody] CreateRoleFromTemplateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TemplateName))
                {
                    return BadRequest(new { message = "Template name is required" });
                }

                var templates = _permissionManager.GetRoleTemplates();
                var template = templates.FirstOrDefault(t => t.Name.Equals(request.TemplateName, StringComparison.OrdinalIgnoreCase));
                
                if (template == null)
                {
                    return NotFound(new { message = $"Template '{request.TemplateName}' not found" });
                }

                var createdRole = await _permissionManager.CreateRoleFromTemplateAsync(template);
                
                if (createdRole != null)
                {
                    return Ok(new { 
                        message = $"Role created from template '{request.TemplateName}'",
                        role = createdRole.ToDto()
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to create role from template"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role from template");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create a custom role with specific permissions
        /// </summary>
        [HttpPost("create-custom-role")]
        [RequireRolesWrite]
        public async Task<IActionResult> CreateCustomRole([FromBody] CreateCustomRoleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Name) || request.Permissions == null || !request.Permissions.Any())
                {
                    return BadRequest(new { message = "Role name and permissions are required" });
                }

                var createdRole = await _permissionManager.CreateCustomRoleAsync(
                    request.Name, 
                    request.Permissions);
                
                if (createdRole != null)
                {
                    return Ok(new { 
                        message = $"Custom role '{request.Name}' created successfully",
                        role = createdRole.ToDto()
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to create custom role. Check if permissions are valid."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating custom role");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update role permissions
        /// </summary>
        [HttpPut("roles/{roleId}/permissions")]
        [RequireRolesWrite]
        public async Task<IActionResult> UpdateRolePermissions(long roleId, [FromBody] UpdateRolePermissionsRequest request)
        {
            try
            {
                if (request.Permissions == null || !request.Permissions.Any())
                {
                    return BadRequest(new { message = "Permissions list is required" });
                }

                var success = await _permissionManager.UpdateRolePermissionsAsync(roleId, request.Permissions);
                
                if (success)
                {
                    return Ok(new { 
                        message = $"Permissions updated for role {roleId}",
                        roleId = roleId,
                        permissionCount = request.Permissions.Length
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to update role permissions"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role permissions");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Validate permission names
        /// </summary>
        [HttpPost("validate-permissions")]
        [RequireRolesRead]
        public IActionResult ValidatePermissions([FromBody] ValidatePermissionsRequest request)
        {
            try
            {
                if (request.PermissionNames == null || !request.PermissionNames.Any())
                {
                    return BadRequest(new { message = "Permission names are required" });
                }

                var validation = _permissionManager.ValidatePermissions(request.PermissionNames);
                
                return Ok(new { 
                    message = "Permission validation completed",
                    isValid = validation.IsValid,
                    validPermissions = request.PermissionNames.Where(p => !validation.MissingPermissions.Contains(p)).ToArray(),
                    invalidPermissions = validation.MissingPermissions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating permissions");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get system information about permissions and roles
        /// </summary>
        [HttpGet("system-info")]
        [RequireRolesRead]
        public async Task<IActionResult> GetSystemInfo()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get counts from database (permissions from code constants)
                var rolesResponse = await client.From<Role>().Get();
                var usersResponse = await client.From<User>().Get();

                var permissionsCount = PermissionConstants.GetAllPermissions().Count;
                var rolesCount = rolesResponse.Models?.Count ?? 0;
                var usersCount = usersResponse.Models?.Count ?? 0;

                // Get available constants info
                var availablePermissions = _permissionManager.GetAvailablePermissions();
                var availableTemplates = _permissionManager.GetRoleTemplates();

                return Ok(new { 
                    message = "System information retrieved",
                    database = new 
                    {
                        permissionsCount = permissionsCount,
                        rolesCount = rolesCount,
                        usersCount = usersCount
                    },
                    constants = new 
                    {
                        availablePermissionsCount = availablePermissions.Values.SelectMany(x => x).Count(),
                        availableRoleTemplatesCount = availableTemplates.Count,
                        resourcesCount = availablePermissions.Keys.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system info");
                return StatusCode(500, new { 
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        #region Games Management

        /// <summary>
        /// Get all games for admin management with category information
        /// </summary>
        [HttpGet("games")]
        [RequireGamesAdmin]
        public async Task<IActionResult> GetAllGamesAdmin()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Fetch all games
                var gamesResponse = await client
                    .From<Game>()
                    .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var games = gamesResponse.Models ?? new List<Game>();
                
                if (games.Any())
                {
                    // Get all unique category IDs from the games
                    var categoryIds = games
                        .Where(g => g.CategoryId.HasValue)
                        .Select(g => g.CategoryId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Fetch all categories in batch
                    var categoriesResponse = await client
                        .From<Category>()
                        .Get();
                    
                    var allCategories = categoriesResponse.Models ?? new List<Category>();
                    var categories = allCategories.Where(cat => categoryIds.Contains(cat.Id)).ToDictionary(cat => cat.Id);
                    
                    // Create extended DTOs with category information
                    var gamesWithCategory = games.Select(game => 
                    {
                        var gameWithCategory = new GameWithCategoryDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            Category = game.CategoryId.HasValue && categories.ContainsKey(game.CategoryId.Value)
                                ? categories[game.CategoryId.Value].ToDto()
                                : null
                        };
                        return gameWithCategory;
                    }).ToList();
                    
                    return Ok(new
                    {
                        message = "Games with categories retrieved successfully for admin",
                        data = gamesWithCategory,
                        count = gamesWithCategory.Count
                    });
                }
                else
                {
                    return Ok(new
                    {
                        message = "No games found",
                        data = new List<GameWithCategoryDto>(),
                        count = 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving games for admin");
                return StatusCode(500, new { message = "Error retrieving games" });
            }
        }

        /// <summary>
        /// Create a new game
        /// </summary>
        [HttpPost("games")]
        [RequireGamesAdmin]
        public async Task<IActionResult> CreateGame([FromBody] CreateGameRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Game name is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Verify category exists if provided
                if (request.CategoryId.HasValue)
                {
                    var categoryCheck = await client
                        .From<Category>()
                        .Where(x => x.Id == request.CategoryId.Value)
                        .Single();
                    
                    if (categoryCheck == null)
                    {
                        return BadRequest(new { message = "Category not found" });
                    }
                }

                var game = new Game
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Cover = request.Cover?.Trim(),
                    CategoryId = request.CategoryId,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await client
                    .From<Game>()
                    .Insert(game);

                var createdGame = result.Models?.FirstOrDefault();
                if (createdGame != null)
                {
                    _logger.LogInformation("Game created successfully: {GameName} (ID: {GameId})", 
                        createdGame.Name, createdGame.Id);
                    
                    return CreatedAtAction(nameof(GetAllGamesAdmin), new { id = createdGame.Id }, 
                        new { 
                            message = "Game created successfully", 
                            data = createdGame.ToDto() 
                        });
                }

                return BadRequest(new { message = "Failed to create game" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game: {GameName}", request.Name);
                return StatusCode(500, new { message = "Error creating game" });
            }
        }

        /// <summary>
        /// Update an existing game
        /// </summary>
        [HttpPut("games/{id}")]
        [RequireGamesAdmin]
        public async Task<IActionResult> UpdateGame(long id, [FromBody] UpdateGameRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Game name is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if game exists
                var existingGame = await client
                    .From<Game>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingGame == null)
                {
                    return NotFound(new { message = "Game not found" });
                }

                // Verify category exists if provided
                if (request.CategoryId.HasValue)
                {
                    var categoryCheck = await client
                        .From<Category>()
                        .Where(x => x.Id == request.CategoryId.Value)
                        .Single();
                    
                    if (categoryCheck == null)
                    {
                        return BadRequest(new { message = "Category not found" });
                    }
                }

                // Update fields
                existingGame.Name = request.Name.Trim();
                existingGame.Description = request.Description?.Trim();
                existingGame.Cover = request.Cover?.Trim();
                existingGame.CategoryId = request.CategoryId;

                var result = await client
                    .From<Game>()
                    .Update(existingGame);

                var updatedGame = result.Models?.FirstOrDefault();
                if (updatedGame != null)
                {
                    _logger.LogInformation("Game updated successfully: {GameName} (ID: {GameId})", 
                        updatedGame.Name, updatedGame.Id);
                    
                    return Ok(new { 
                        message = "Game updated successfully", 
                        data = updatedGame.ToDto() 
                    });
                }

                return BadRequest(new { message = "Failed to update game" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating game with ID: {GameId}", id);
                return StatusCode(500, new { message = "Error updating game" });
            }
        }

        /// <summary>
        /// Delete a game (cascades to game keys)
        /// </summary>
        [HttpDelete("games/{id}")]
        [RequireGamesAdmin]
        public async Task<IActionResult> DeleteGame(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if game exists
                var existingGame = await client
                    .From<Game>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingGame == null)
                {
                    return NotFound(new { message = "Game not found" });
                }

                // Delete game (this will cascade delete game keys due to FK constraint)
                await client
                    .From<Game>()
                    .Where(x => x.Id == id)
                    .Delete();

                _logger.LogInformation("Game deleted successfully: {GameName} (ID: {GameId})", 
                    existingGame.Name, existingGame.Id);

                return Ok(new { 
                    message = "Game deleted successfully",
                    deletedGame = existingGame.ToDto()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting game with ID: {GameId}", id);
                return StatusCode(500, new { message = "Error deleting game" });
            }
        }

        #endregion
    }

    // Request DTOs
    public class CreateRoleFromTemplateRequest
    {
        public string TemplateName { get; set; } = string.Empty;
    }

    public class CreateCustomRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }

    // Games DTOs
    public class CreateGameRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("cover")]
        public string? Cover { get; set; }
        
        [JsonPropertyName("category_id")]
        public long? CategoryId { get; set; }
    }

    public class UpdateGameRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("cover")]
        public string? Cover { get; set; }
        
        [JsonPropertyName("category_id")]
        public long? CategoryId { get; set; }
    }

    public class UpdateRolePermissionsRequest
    {
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }

    public class ValidatePermissionsRequest
    {
        public string[] PermissionNames { get; set; } = Array.Empty<string>();
    }
}
