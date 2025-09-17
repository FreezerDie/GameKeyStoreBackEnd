using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Authorization;
using GameKeyStore.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminGameKeysController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<AdminGameKeysController> _logger;

        public AdminGameKeysController(SupabaseService supabaseService, ILogger<AdminGameKeysController> logger)
        {
            _supabaseService = supabaseService;
            _logger = logger;
        }

        #region Game Keys Management

        /// <summary>
        /// Get all game keys for admin management
        /// </summary>
        [HttpGet("game-keys")]
        [RequireGameKeysAdmin]
        public async Task<IActionResult> GetAllGameKeysAdmin([FromQuery] long? gameId = null, [FromQuery] bool includeGame = true)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Get game keys with optional filtering
                var gameKeysResponse = gameId.HasValue 
                    ? await client.From<GameKey>().Where(x => x.GameId == gameId.Value)
                        .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get()
                    : await client.From<GameKey>()
                        .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get();
                
                var gameKeys = gameKeysResponse.Models ?? new List<GameKey>();
                
                if (includeGame && gameKeys.Any())
                {
                    // Get all unique game IDs from the game keys
                    var gameIds = gameKeys
                        .Where(gk => gk.GameId.HasValue)
                        .Select(gk => gk.GameId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Fetch games in batch
                    var gamesResponse = await client
                        .From<Game>()
                        .Get();
                    
                    var allGames = gamesResponse.Models ?? new List<Game>();
                    var games = allGames.Where(game => gameIds.Contains(game.Id)).ToDictionary(game => game.Id);
                    
                    // Create extended DTOs with game information
                    var gameKeysWithGame = gameKeys.Select(gameKey => 
                    {
                        var gameKeyWithGame = new GameKeyWithGameDto
                        {
                            Id = gameKey.Id,
                            CreatedAt = gameKey.CreatedAt,
                            Key = gameKey.Key,
                            GameId = gameKey.GameId,
                            Price = gameKey.Price,
                            KeyType = gameKey.KeyType,
                            Game = gameKey.GameId.HasValue && games.ContainsKey(gameKey.GameId.Value)
                                ? games[gameKey.GameId.Value].ToDto()
                                : null
                        };
                        return gameKeyWithGame;
                    }).ToList();
                    
                    return Ok(new
                    {
                        message = "Game keys with game information retrieved successfully",
                        data = gameKeysWithGame,
                        count = gameKeysWithGame.Count(),
                        filteredBy = gameId.HasValue ? $"game_id: {gameId}" : null
                    });
                }
                else
                {
                    // Return simple DTOs without game information
                    var gameKeyDtos = gameKeys.Select(gk => gk.ToDto()).ToList();
                    
                    return Ok(new
                    {
                        message = "Game keys retrieved successfully",
                        data = gameKeyDtos,
                        count = gameKeyDtos.Count(),
                        filteredBy = gameId.HasValue ? $"game_id: {gameId}" : null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving game keys for admin");
                return StatusCode(500, new { message = "Error retrieving game keys" });
            }
        }

        /// <summary>
        /// Create a new game key
        /// </summary>
        [HttpPost("game-keys")]
        [RequireGameKeysAdmin]
        public async Task<IActionResult> CreateGameKey([FromBody] CreateGameKeyRequest request)
        {
            try
            {
                // Check model validation
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Validation failed", 
                        errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                            .ToDictionary(x => x.Key, x => x.Value?.Errors.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>())
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Key))
                {
                    return BadRequest(new { message = "Game key is required" });
                }

                if (!request.GameId.HasValue)
                {
                    return BadRequest(new { message = "Game ID is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Verify game exists
                var gameCheck = await client
                    .From<Game>()
                    .Where(x => x.Id == request.GameId.Value)
                    .Single();
                
                if (gameCheck == null)
                {
                    return BadRequest(new { message = "Game not found" });
                }

                // Check if key already exists
                var trimmedKey = request.Key.Trim();
                var existingKey = await client
                    .From<GameKey>()
                    .Where(x => x.Key == trimmedKey)
                    .Single();

                if (existingKey != null)
                {
                    return BadRequest(new { message = "Game key already exists" });
                }

                var gameKey = new GameKey
                {
                    Key = trimmedKey,
                    GameId = request.GameId.Value,
                    Price = request.Price,
                    KeyType = request.KeyType,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await client
                    .From<GameKey>()
                    .Insert(gameKey);

                var createdGameKey = result.Models?.FirstOrDefault();
                if (createdGameKey != null)
                {
                    _logger.LogInformation("Game key created successfully for Game ID: {GameId}", 
                        createdGameKey.GameId);
                    
                    return CreatedAtAction(nameof(GetAllGameKeysAdmin), new { id = createdGameKey.Id }, 
                        new { 
                            message = "Game key created successfully", 
                            data = createdGameKey.ToDto() 
                        });
                }

                return BadRequest(new { message = "Failed to create game key" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game key for Game ID: {GameId}", request.GameId);
                return StatusCode(500, new { message = "Error creating game key" });
            }
        }

        /// <summary>
        /// Update an existing game key
        /// </summary>
        [HttpPut("game-keys/{id}")]
        [RequireGameKeysAdmin]
        public async Task<IActionResult> UpdateGameKey(long id, [FromBody] UpdateGameKeyRequest request)
        {
            try
            {
                // Check model validation
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Validation failed", 
                        errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                            .ToDictionary(x => x.Key, x => x.Value?.Errors.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>())
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Key))
                {
                    return BadRequest(new { message = "Game key is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if game key exists
                var existingGameKey = await client
                    .From<GameKey>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingGameKey == null)
                {
                    return NotFound(new { message = "Game key not found" });
                }

                // Verify game exists if provided
                if (request.GameId.HasValue)
                {
                    var gameCheck = await client
                        .From<Game>()
                        .Where(x => x.Id == request.GameId.Value)
                        .Single();
                    
                    if (gameCheck == null)
                    {
                        return BadRequest(new { message = "Game not found" });
                    }
                }

                // Check if key already exists (excluding current key)
                var trimmedKey = request.Key.Trim();
                var duplicateKey = await client
                    .From<GameKey>()
                    .Where(x => x.Key == trimmedKey && x.Id != id)
                    .Single();

                if (duplicateKey != null)
                {
                    return BadRequest(new { message = "Game key already exists" });
                }

                // Update fields
                existingGameKey.Key = trimmedKey;
                if (request.GameId.HasValue)
                {
                    existingGameKey.GameId = request.GameId.Value;
                }
                if (request.Price.HasValue)
                {
                    existingGameKey.Price = request.Price.Value;
                }
                if (!string.IsNullOrEmpty(request.KeyType))
                {
                    existingGameKey.KeyType = request.KeyType;
                }

                var result = await client
                    .From<GameKey>()
                    .Update(existingGameKey);

                var updatedGameKey = result.Models?.FirstOrDefault();
                if (updatedGameKey != null)
                {
                    _logger.LogInformation("Game key updated successfully (ID: {GameKeyId})", 
                        updatedGameKey.Id);
                    
                    return Ok(new { 
                        message = "Game key updated successfully", 
                        data = updatedGameKey.ToDto() 
                    });
                }

                return BadRequest(new { message = "Failed to update game key" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating game key with ID: {GameKeyId}", id);
                return StatusCode(500, new { message = "Error updating game key" });
            }
        }

        /// <summary>
        /// Delete a game key
        /// </summary>
        [HttpDelete("game-keys/{id}")]
        [RequireGameKeysAdmin]
        public async Task<IActionResult> DeleteGameKey(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if game key exists
                var existingGameKey = await client
                    .From<GameKey>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingGameKey == null)
                {
                    return NotFound(new { message = "Game key not found" });
                }

                // Delete game key
                await client
                    .From<GameKey>()
                    .Where(x => x.Id == id)
                    .Delete();

                _logger.LogInformation("Game key deleted successfully (ID: {GameKeyId})", id);

                return Ok(new { 
                    message = "Game key deleted successfully",
                    deletedGameKey = existingGameKey.ToDto()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting game key with ID: {GameKeyId}", id);
                return StatusCode(500, new { message = "Error deleting game key" });
            }
        }

        /// <summary>
        /// Bulk create game keys for a specific game
        /// </summary>
        [HttpPost("game-keys/bulk")]
        [RequireGameKeysAdmin]
        public async Task<IActionResult> BulkCreateGameKeys([FromBody] BulkCreateGameKeysRequest request)
        {
            try
            {
                // Check model validation
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Validation failed", 
                        errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                            .ToDictionary(x => x.Key, x => x.Value?.Errors.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>())
                    });
                }

                if (!request.GameId.HasValue)
                {
                    return BadRequest(new { message = "Game ID is required" });
                }

                if (request.Keys == null || !request.Keys.Any())
                {
                    return BadRequest(new { message = "At least one key is required" });
                }

                var uniqueKeys = request.Keys.Where(k => !string.IsNullOrWhiteSpace(k))
                                           .Select(k => k.Trim())
                                           .Distinct()
                                           .ToList();

                if (!uniqueKeys.Any())
                {
                    return BadRequest(new { message = "No valid keys provided" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Verify game exists
                var gameCheck = await client
                    .From<Game>()
                    .Where(x => x.Id == request.GameId.Value)
                    .Single();
                
                if (gameCheck == null)
                {
                    return BadRequest(new { message = "Game not found" });
                }

                // Check for existing keys (get all keys for this game and filter)
                var existingKeysResponse = await client
                    .From<GameKey>()
                    .Get();

                var existingKeys = existingKeysResponse.Models?
                    .Where(k => uniqueKeys.Contains(k.Key ?? ""))
                    .Select(k => k.Key ?? "")
                    .ToHashSet() ?? new HashSet<string>();
                var newKeys = uniqueKeys.Where(k => !existingKeys.Contains(k)).ToList();

                if (!newKeys.Any())
                {
                    return BadRequest(new { message = "All keys already exist" });
                }

                // Create game keys
                var gameKeys = newKeys.Select(key => new GameKey
                {
                    Key = key,
                    GameId = request.GameId.Value,
                    Price = request.Price,
                    KeyType = request.KeyType,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                var result = await client
                    .From<GameKey>()
                    .Insert(gameKeys);

                var createdGameKeys = result.Models?.Select(gk => gk.ToDto()) ?? new List<GameKeyDto>();

                _logger.LogInformation("Bulk created {Count} game keys for Game ID: {GameId}", 
                    createdGameKeys.Count(), request.GameId);

                return Ok(new
                {
                    message = $"Successfully created {createdGameKeys.Count()} game keys",
                    data = createdGameKeys,
                    duplicatesSkipped = existingKeys.Count,
                    totalProcessed = uniqueKeys.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating game keys for Game ID: {GameId}", request.GameId);
                return StatusCode(500, new { message = "Error creating game keys" });
            }
        }

        #endregion
    }

    #region Game Keys Request DTOs

    public class CreateGameKeyRequest
    {
        [Required(ErrorMessage = "Game key is required")]
        public string Key { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Game ID is required")]
        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }
        
        [JsonPropertyName("price")]
        public float? Price { get; set; }
        
        [JsonPropertyName("key_type")]
        public string? KeyType { get; set; }
    }

    public class UpdateGameKeyRequest
    {
        [Required(ErrorMessage = "Game key is required")]
        public string Key { get; set; } = string.Empty;
        
        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }
        
        [JsonPropertyName("price")]
        public float? Price { get; set; }
        
        [JsonPropertyName("key_type")]
        public string? KeyType { get; set; }
    }

    public class BulkCreateGameKeysRequest
    {
        [Required(ErrorMessage = "Game ID is required")]
        [JsonPropertyName("game_id")]
        public long? GameId { get; set; }
        
        [Required(ErrorMessage = "At least one key is required")]
        public List<string> Keys { get; set; } = new List<string>();
        
        [JsonPropertyName("price")]
        public float? Price { get; set; }
        
        [JsonPropertyName("key_type")]
        public string? KeyType { get; set; }
    }

    #endregion
}
