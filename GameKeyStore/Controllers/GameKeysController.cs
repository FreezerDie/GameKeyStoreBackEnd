using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;

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
        /// Get all game keys from the database
        /// </summary>
        /// <returns>List of game keys</returns>
        [HttpGet]
        public async Task<IActionResult> GetGameKeys()
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Fetch data from the game_keys table using official Supabase client
                var response = await client
                    .From<GameKey>()
                    .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                
                // Convert BaseModel to DTO for serialization
                var gameKeyDtos = response.Models?.Select(x => x.ToDto()).ToList();
                
                return Ok(new { 
                    message = "Data fetched from database", 
                    count = gameKeyDtos?.Count ?? 0,
                    data = gameKeyDtos
                });
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
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = mockGameKeys.Count,
                    data = mockGameKeys
                });
            }
        }

        /// <summary>
        /// Get a specific game key by ID
        /// </summary>
        /// <param name="id">The game key ID</param>
        /// <returns>Game key details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGameKey(long id)
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
                    return Ok(new { 
                        message = "Game key found", 
                        data = gameKey.ToDto()
                    });
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
    }
}
