using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;

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
        /// <param name="includeGameKeys">Whether to include game keys information in response</param>
        /// <returns>List of games</returns>
        [HttpGet]
        public async Task<IActionResult> GetGames([FromQuery] long? categoryId = null, [FromQuery] bool includeCategory = false, [FromQuery] bool includeGameKeys = false)
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
                        .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
                        .Get()
                    : await client
                        .From<Game>()
                        .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
                        .Get();
                
                var games = gamesResponse.Models ?? new List<Game>();
                
                // Prepare data based on requested includes
                Dictionary<long, Category>? categories = null;
                Dictionary<long, List<GameKey>>? gameKeysByGameId = null;
                
                // Fetch categories if requested
                if (includeCategory && games.Any())
                {
                    var categoryIds = games
                        .Where(g => g.CategoryId.HasValue)
                        .Select(g => g.CategoryId!.Value)
                        .Distinct()
                        .ToList();
                    
                    var categoriesResponse = await client
                        .From<Category>()
                        .Get();
                    
                    var allCategories = categoriesResponse.Models ?? new List<Category>();
                    categories = allCategories.Where(cat => categoryIds.Contains(cat.Id)).ToDictionary(cat => cat.Id);
                }
                
                // Fetch game keys if requested
                if (includeGameKeys && games.Any())
                {
                    var gameIds = games.Select(g => g.Id).ToList();
                    var gameKeysResponse = await client
                        .From<GameKey>()
                        .Get();
                    
                    var allGameKeys = gameKeysResponse.Models ?? new List<GameKey>();
                    gameKeysByGameId = allGameKeys
                        .Where(gk => gameIds.Contains(gk.GameId ?? 0))
                        .GroupBy(gk => gk.GameId ?? 0)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
                
                // Create appropriate DTOs based on requested includes
                if (includeCategory && includeGameKeys)
                {
                    var gamesWithCategoryAndKeys = games.Select(game => 
                    {
                        var gameKeys = gameKeysByGameId?.ContainsKey(game.Id) == true 
                            ? gameKeysByGameId[game.Id].Select(gk => gk.ToDto()).ToList() 
                            : new List<GameKeyDto>();
                            
                        return new GameWithCategoryAndKeysDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            Category = game.CategoryId.HasValue && categories?.ContainsKey(game.CategoryId.Value) == true
                                ? categories[game.CategoryId.Value].ToDto()
                                : null,
                            GameKeys = gameKeys
                        };
                    }).ToList();
                    
                    return Ok(new { 
                        message = categoryId.HasValue 
                            ? $"Games filtered by category {categoryId} with category and keys information fetched from database" 
                            : "Games with category and keys information fetched from database",
                        count = gamesWithCategoryAndKeys.Count,
                        categoryFilter = categoryId,
                        data = gamesWithCategoryAndKeys
                    });
                }
                else if (includeCategory)
                {
                    var gamesWithCategory = games.Select(game => 
                    {
                        return new GameWithCategoryDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            Category = game.CategoryId.HasValue && categories?.ContainsKey(game.CategoryId.Value) == true
                                ? categories[game.CategoryId.Value].ToDto()
                                : null
                        };
                    }).ToList();
                    
                    return Ok(new { 
                        message = categoryId.HasValue 
                            ? $"Games filtered by category {categoryId} with category information fetched from database" 
                            : "Games with category information fetched from database",
                        count = gamesWithCategory.Count,
                        categoryFilter = categoryId,
                        data = gamesWithCategory
                    });
                }
                else if (includeGameKeys)
                {
                    var gamesWithKeys = games.Select(game => 
                    {
                        var gameKeys = gameKeysByGameId?.ContainsKey(game.Id) == true 
                            ? gameKeysByGameId[game.Id].Select(gk => gk.ToDto()).ToList() 
                            : new List<GameKeyDto>();
                            
                        return new GameWithKeysDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            GameKeys = gameKeys
                        };
                    }).ToList();
                    
                    return Ok(new { 
                        message = categoryId.HasValue 
                            ? $"Games filtered by category {categoryId} with keys information fetched from database" 
                            : "Games with keys information fetched from database",
                        count = gamesWithKeys.Count,
                        categoryFilter = categoryId,
                        data = gamesWithKeys
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
                
                // Create mock game keys for demo purposes
                var mockGameKeys = new Dictionary<long, List<GameKeyDto>>
                {
                    {1, new List<GameKeyDto> { new GameKeyDto { Id = 1, GameId = 1, Price = 59.99f, KeyType = "Steam", Key = "XXXX-XXXX-XXXX", CreatedAt = DateTime.Now } }},
                    {2, new List<GameKeyDto> { new GameKeyDto { Id = 2, GameId = 2, Price = 39.99f, KeyType = "Steam", Key = "XXXX-XXXX-XXXX", CreatedAt = DateTime.Now } }},
                    {3, new List<GameKeyDto> { new GameKeyDto { Id = 3, GameId = 3, Price = 49.99f, KeyType = "Steam", Key = "XXXX-XXXX-XXXX", CreatedAt = DateTime.Now } }},
                    {4, new List<GameKeyDto> { new GameKeyDto { Id = 4, GameId = 4, Price = 69.99f, KeyType = "Origin", Key = "XXXX-XXXX-XXXX", CreatedAt = DateTime.Now } }},
                    {5, new List<GameKeyDto> { new GameKeyDto { Id = 5, GameId = 5, Price = 44.99f, KeyType = "Uplay", Key = "XXXX-XXXX-XXXX", CreatedAt = DateTime.Now } }}
                };
                
                // Return appropriate mock data based on requested includes
                if (includeGameKeys && !includeCategory)
                {
                    var mockGamesWithKeys = mockGames.Select(game => new GameWithKeysDto
                    {
                        Id = game.Id,
                        CreatedAt = game.CreatedAt,
                        Name = game.Name,
                        Description = game.Description,
                        Cover = game.Cover,
                        CategoryId = game.CategoryId,
                        GameKeys = mockGameKeys.ContainsKey(game.Id) ? mockGameKeys[game.Id] : new List<GameKeyDto>()
                    }).ToList();
                    
                    return Ok(new { 
                        message = "Using fallback data with game keys - database connection failed", 
                        error = ex.Message,
                        count = mockGamesWithKeys.Count,
                        categoryFilter = categoryId,
                        data = mockGamesWithKeys
                    });
                }
                else
                {
                    return Ok(new { 
                        message = "Using fallback data - database connection failed", 
                        error = ex.Message,
                        count = mockGames.Count,
                        categoryFilter = categoryId,
                        data = mockGames
                    });
                }
            }
        }

        /// <summary>
        /// Get a specific game by ID
        /// </summary>
        /// <param name="id">The game ID</param>
        /// <param name="includeCategory">Whether to include category information in response</param>
        /// <param name="includeGameKeys">Whether to include game keys information in response</param>
        /// <returns>Game details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(long id, [FromQuery] bool includeCategory = false, [FromQuery] bool includeGameKeys = true)
        {
            try
            {
                // Debug logging to see what parameters are being received
                Console.WriteLine($"🔍 GetGame called with id={id}, includeCategory={includeCategory}, includeGameKeys={includeGameKeys}");
                
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
                    Category? category = null;
                    List<GameKey>? gameKeys = null;
                    
                    // Fetch category information if requested
                    if (includeCategory && game.CategoryId.HasValue)
                    {
                        var categoryResponse = await client
                            .From<Category>()
                            .Where(x => x.Id == game.CategoryId.Value)
                            .Get();
                        
                        category = categoryResponse.Models?.FirstOrDefault();
                    }
                    
                    // Fetch game keys if requested
                    if (includeGameKeys)
                    {
                        Console.WriteLine($"🔍 Fetching game keys for game ID: {id}");
                        try
                        {
                            var gameKeysResponse = await client
                                .From<GameKey>()
                                .Where(x => x.GameId == id)
                                .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                                .Get();
                            
                            gameKeys = gameKeysResponse.Models ?? new List<GameKey>();
                            Console.WriteLine($"🔍 Found {gameKeys.Count} game keys for game ID: {id}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Error fetching game keys: {ex.Message}");
                            // If there's an error fetching game keys, return empty list
                            gameKeys = new List<GameKey>();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"🔍 includeGameKeys is false, skipping game keys fetch");
                    }
                    
                    // Return appropriate DTO based on what's included
                    Console.WriteLine($"🔍 Decision time: includeCategory={includeCategory}, includeGameKeys={includeGameKeys}");
                    
                    if (includeCategory && includeGameKeys)
                    {
                        Console.WriteLine($"🔍 Taking path: Both category and game keys");
                        var gameWithCategoryAndKeys = new GameWithCategoryAndKeysDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            Category = category?.ToDto(),
                            GameKeys = gameKeys?.Any() == true 
                                ? gameKeys.Select(x => x.ToDto()).ToList() 
                                : new List<GameKeyDto>()
                        };
                        
                        return Ok(new { 
                            message = gameWithCategoryAndKeys.GameKeys.Any() 
                                ? "Game with category and keys found" 
                                : "Game with category found (no keys available)", 
                            data = gameWithCategoryAndKeys
                        });
                    }
                    else if (includeCategory)
                    {
                        Console.WriteLine($"🔍 Taking path: Category only");
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
                    else if (includeGameKeys)
                    {
                        Console.WriteLine($"🔍 Taking path: Game keys only");
                        var gameWithKeys = new GameWithKeysDto
                        {
                            Id = game.Id,
                            CreatedAt = game.CreatedAt,
                            Name = game.Name,
                            Description = game.Description,
                            Cover = game.Cover,
                            CategoryId = game.CategoryId,
                            GameKeys = gameKeys?.Any() == true 
                                ? gameKeys.Select(x => x.ToDto()).ToList() 
                                : new List<GameKeyDto>()
                        };
                        
                        return Ok(new { 
                            message = gameWithKeys.GameKeys.Any() 
                                ? "Game with keys found" 
                                : "Game found (no keys available)", 
                            data = gameWithKeys
                        });
                    }
                    else
                    {
                        Console.WriteLine($"🔍 Taking path: Basic game info only");
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
                    .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                var games = gamesResponse.Models ?? new List<Game>();
                
                // Fetch all active categories
                var categoriesResponse = await client
                    .From<Category>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
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
