using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameKeysController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;

        public GameKeysController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// 
        /// Get all game keys from the database with optional game filtering
        /// </summary>
        /// <param name="gameId">Optional game ID to filter game keys</param>
        /// <param name="includeGame">Whether to include game information in response</param>
        /// <returns>List of game keys</returns>
        [HttpGet]
        [RequireGameKeysRead]
        public async Task<IActionResult> GetGameKeys([FromQuery] long? gameId = null, [FromQuery] bool includeGame = false)
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Execute query with optional game filter
                var gameKeysResponse = gameId.HasValue
                    ? await client
                        .From<GameKey>()
                        .Where(x => x.GameId == gameId.Value)
                        .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get()
                    : await client
                        .From<GameKey>()
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
                    
                    return Ok(new { 
                        message = gameId.HasValue 
                            ? $"Game keys filtered by game {gameId} fetched from database" 
                            : "Game keys with game information fetched from database",
                        count = gameKeysWithGame.Count,
                        gameFilter = gameId,
                        data = gameKeysWithGame
                    });
                }
                else
                {
                    // Convert to simple DTOs
                    var gameKeyDtos = gameKeys.Select(x => x.ToDto()).ToList();
                    
                    return Ok(new { 
                        message = gameId.HasValue 
                            ? $"Game keys filtered by game {gameId} fetched from database" 
                            : "Game keys fetched from database",
                        count = gameKeyDtos.Count,
                        gameFilter = gameId,
                        data = gameKeyDtos
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                // Return mock data as fallback
                var mockGameKeys = new List<GameKeyDto>
                {
                    new GameKeyDto { Id = 1, CreatedAt = DateTime.Now.AddDays(-5), Key = "ABCD-EFGH-1234-5678", GameId = 1 },
                    new GameKeyDto { Id = 2, CreatedAt = DateTime.Now.AddDays(-3), Key = "IJKL-MNOP-5678-9012", GameId = 2 },
                    new GameKeyDto { Id = 3, CreatedAt = DateTime.Now.AddDays(-1), Key = "QRST-UVWX-3456-7890", GameId = 1 },
                    new GameKeyDto { Id = 4, CreatedAt = DateTime.Now, Key = "YZAB-CDEF-1111-2222", GameId = 3 }
                };
                
                // Apply game filter to mock data if requested
                if (gameId.HasValue)
                {
                    mockGameKeys = mockGameKeys.Where(gk => gk.GameId == gameId.Value).ToList();
                }
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = mockGameKeys.Count,
                    gameFilter = gameId,
                    data = mockGameKeys
                });
            }
        }

        /// <summary>
        /// Get a specific game key by ID
        /// </summary>
        /// <param name="id">The game key ID</param>
        /// <param name="includeGame">Whether to include game information in response</param>
        /// <returns>Game key details</returns>
        [HttpGet("{id}")]
        [RequireGameKeysRead]
        public async Task<IActionResult> GetGameKey(long id, [FromQuery] bool includeGame = false)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch specific game key by ID using official Supabase client
                var response = await client
                    .From<GameKey>()
                    .Where(x => x.Id == id)
                    .Get();
                
                var gameKey = response.Models?.FirstOrDefault();
                
                if (gameKey != null)
                {
                    if (includeGame && gameKey.GameId.HasValue)
                    {
                        // Fetch game information
                        var gameResponse = await client
                            .From<Game>()
                            .Where(x => x.Id == gameKey.GameId.Value)
                            .Get();
                        
                        var game = gameResponse.Models?.FirstOrDefault();
                        
                        var gameKeyWithGame = new GameKeyWithGameDto
                        {
                            Id = gameKey.Id,
                            CreatedAt = gameKey.CreatedAt,
                            Key = gameKey.Key,
                            GameId = gameKey.GameId,
                            Price = gameKey.Price,
                            KeyType = gameKey.KeyType,
                            Game = game?.ToDto()
                        };
                        
                        return Ok(new { 
                            message = "Game key with game information found", 
                            data = gameKeyWithGame
                        });
                    }
                    else
                    {
                        return Ok(new { 
                            message = "Game key found", 
                            data = gameKey.ToDto()
                        });
                    }
                }
                
                return NotFound(new { 
                    message = $"Game key with ID {id} not found" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching game key", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get all game keys for a specific game
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="includeGame">Whether to include game information in response</param>
        /// <returns>List of game keys for the specified game</returns>
        [HttpGet("by-game/{gameId}")]
        [RequireGameKeysRead]
        public async Task<IActionResult> GetGameKeysByGame(long gameId, [FromQuery] bool includeGame = false)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch game keys for specific game
                var gameKeysResponse = await client
                    .From<GameKey>()
                    .Where(x => x.GameId == gameId)
                    .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                
                var gameKeys = gameKeysResponse.Models ?? new List<GameKey>();
                
                if (includeGame && gameKeys.Any())
                {
                    // Fetch the game information
                    var gameResponse = await client
                        .From<Game>()
                        .Where(x => x.Id == gameId)
                        .Get();
                    
                    var game = gameResponse.Models?.FirstOrDefault();
                    
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
                            Game = game?.ToDto()
                        };
                        return gameKeyWithGame;
                    }).ToList();
                    
                    return Ok(new { 
                        message = $"Game keys for game {gameId} with game information fetched from database",
                        gameId = gameId,
                        count = gameKeysWithGame.Count,
                        data = gameKeysWithGame
                    });
                }
                else
                {
                    // Convert to simple DTOs
                    var gameKeyDtos = gameKeys.Select(x => x.ToDto()).ToList();
                    
                    return Ok(new { 
                        message = $"Game keys for game {gameId} fetched from database",
                        gameId = gameId,
                        count = gameKeyDtos.Count,
                        data = gameKeyDtos
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                return BadRequest(new { 
                    message = $"Error fetching game keys for game {gameId}", 
                    error = ex.Message 
                });
            }
        }
    }
}
