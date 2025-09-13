using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;

        public GamesController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// Get all games from the database with optional category filtering
        /// </summary>
        /// <param name="categoryId">Optional category ID to filter games</param>
        /// <param name="includeCategory">Whether to include category information in response</param>
        /// <returns>List of games</returns>
        [HttpGet]
        public async Task<IActionResult> GetGames([FromQuery] long? categoryId = null, [FromQuery] bool includeCategory = false)
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Execute query with optional category filter
                var gamesResponse = categoryId.HasValue
                    ? await client
                        .From<Game>()
                        .Where(x => x.CategoryId == categoryId.Value)
                        .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                        .Get()
                    : await client
                        .From<Game>()
                        .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                        .Get();
                
                var games = gamesResponse.Models ?? new List<Game>();
                
                if (includeCategory && games.Any())
                {
                    // Get all unique category IDs from the games
                    var categoryIds = games
                        .Where(g => g.CategoryId.HasValue)
                        .Select(g => g.CategoryId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Fetch categories in batch
                    var categoriesResponse = await client
                        .From<Category>()
                        .Where(c => categoryIds.Contains(c.Id))
                        .Get();
                    
                    var categories = categoriesResponse.Models?.ToDictionary(c => c.Id) ?? new Dictionary<long, Category>();
                    
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
                    
                    return Ok(new { 
                        message = categoryId.HasValue 
                            ? $"Games filtered by category {categoryId} fetched from database" 
                            : "Games with category information fetched from database",
                        count = gamesWithCategory.Count,
                        categoryFilter = categoryId,
                        data = gamesWithCategory
                    });
                }
                else
                {
                    // Convert to simple DTOs
                    var gameDtos = games.Select(x => x.ToDto()).ToList();
                    
                    return Ok(new { 
                        message = categoryId.HasValue 
                            ? $"Games filtered by category {categoryId} fetched from database" 
                            : "Games fetched from database",
                        count = gameDtos.Count,
                        categoryFilter = categoryId,
                        data = gameDtos
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                // Return mock data as fallback
                var mockGames = new List<GameDto>
                {
                    new GameDto { 
                        Id = 1, 
                        CreatedAt = DateTime.Now.AddDays(-10), 
                        Name = "Call of Duty: Modern Warfare", 
                        Description = "Intense first-person shooter with realistic combat scenarios", 
                        Cover = "cod_mw_cover.jpg",
                        CategoryId = 1 // Action
                    },
                    new GameDto { 
                        Id = 2, 
                        CreatedAt = DateTime.Now.AddDays(-8), 
                        Name = "The Witcher 3: Wild Hunt", 
                        Description = "Epic fantasy RPG with immersive storytelling", 
                        Cover = "witcher3_cover.jpg",
                        CategoryId = 2 // RPG
                    },
                    new GameDto { 
                        Id = 3, 
                        CreatedAt = DateTime.Now.AddDays(-6), 
                        Name = "Civilization VI", 
                        Description = "Turn-based strategy game for building civilizations", 
                        Cover = "civ6_cover.jpg",
                        CategoryId = 3 // Strategy
                    },
                    new GameDto { 
                        Id = 4, 
                        CreatedAt = DateTime.Now.AddDays(-4), 
                        Name = "FIFA 24", 
                        Description = "Latest football simulation game", 
                        Cover = "fifa24_cover.jpg",
                        CategoryId = 4 // Sports
                    },
                    new GameDto { 
                        Id = 5, 
                        CreatedAt = DateTime.Now.AddDays(-2), 
                        Name = "Assassin's Creed Valhalla", 
                        Description = "Viking adventure with stealth and combat", 
                        Cover = "ac_valhalla_cover.jpg",
                        CategoryId = 1 // Action
                    }
                };
                
                // Apply category filter to mock data if requested
                if (categoryId.HasValue)
                {
                    mockGames = mockGames.Where(g => g.CategoryId == categoryId.Value).ToList();
                }
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = mockGames.Count,
                    categoryFilter = categoryId,
                    data = mockGames
                });
            }
        }

        /// <summary>
        /// Get a specific game by ID
        /// </summary>
        /// <param name="id">The game ID</param>
        /// <param name="includeCategory">Whether to include category information in response</param>
        /// <returns>Game details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(long id, [FromQuery] bool includeCategory = false)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch specific game by ID
                var response = await client
                    .From<Game>()
                    .Where(x => x.Id == id)
                    .Get();
                
                var game = response.Models?.FirstOrDefault();
                
                if (game != null)
                {
                    if (includeCategory && game.CategoryId.HasValue)
                    {
                        // Fetch category information
                        var categoryResponse = await client
                            .From<Category>()
                            .Where(c => c.Id == game.CategoryId.Value)
                            .Get();
                        
                        var category = categoryResponse.Models?.FirstOrDefault();
                        
                        var gameWithCategory = new GameWithCategoryDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            Category = category?.ToDto()
                        };
                        
                        return Ok(new { 
                            message = "Game with category found", 
                            data = gameWithCategory
                        });
                    }
                    else
                    {
                        return Ok(new { 
                            message = "Game found", 
                            data = game.ToDto()
                        });
                    }
                }
                
                return NotFound(new { 
                    message = $"Game with ID {id} not found" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching game", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get games grouped by category
        /// </summary>
        /// <returns>Games grouped by categories</returns>
        [HttpGet("by-category")]
        public async Task<IActionResult> GetGamesByCategory()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch all games
                var gamesResponse = await client
                    .From<Game>()
                    .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                var games = gamesResponse.Models ?? new List<Game>();
                
                // Fetch all active categories
                var categoriesResponse = await client
                    .From<Category>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                var categories = categoriesResponse.Models ?? new List<Category>();
                
                // Group games by category
                var gamesByCategory = categories.Select(category => new
                {
                    Category = (CategoryDto?)category.ToDto(),
                    Games = games
                        .Where(g => g.CategoryId == category.Id)
                        .Select(g => g.ToDto())
                        .ToList()
                }).ToList();
                
                // Also include games without category
                var uncategorizedGames = games
                    .Where(g => !g.CategoryId.HasValue)
                    .Select(g => g.ToDto())
                    .ToList();
                
                if (uncategorizedGames.Any())
                {
                    gamesByCategory.Add(new
                    {
                        Category = (CategoryDto?)null,
                        Games = uncategorizedGames
                    });
                }
                
                return Ok(new { 
                    message = "Games grouped by category fetched from database", 
                    categoriesCount = categories.Count,
                    totalGamesCount = games.Count,
                    data = gamesByCategory
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                return BadRequest(new { 
                    message = "Error fetching games by category", 
                    error = ex.Message 
                });
            }
        }
    }
}
