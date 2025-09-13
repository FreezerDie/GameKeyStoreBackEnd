using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;

        public CategoriesController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// Get all categories from the database
        /// </summary>
        /// <returns>List of categories</returns>
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Fetch data from the categories table using official Supabase client
                var response = await client
                    .From<Category>()
                    .Where(x => x.IsActive == true)  // Only fetch active categories
                    .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                // Convert BaseModel to DTO for serialization
                var categoryDtos = response.Models?.Select(x => x.ToDto()).ToList();
                
                return Ok(new { 
                    message = "Categories fetched from database", 
                    count = categoryDtos?.Count ?? 0,
                    data = categoryDtos
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                // Return mock data as fallback
                var mockCategories = new List<CategoryDto>
                {
                    new CategoryDto { 
                        Id = 1, 
                        CreatedAt = DateTime.Now.AddDays(-10), 
                        Name = "Action", 
                        Description = "Action games with intense gameplay", 
                        Cover = "action_cover.jpg",
                        IsActive = true 
                    },
                    new CategoryDto { 
                        Id = 2, 
                        CreatedAt = DateTime.Now.AddDays(-8), 
                        Name = "RPG", 
                        Description = "Role-playing games", 
                        Cover = "rpg_cover.jpg",
                        IsActive = true 
                    },
                    new CategoryDto { 
                        Id = 3, 
                        CreatedAt = DateTime.Now.AddDays(-6), 
                        Name = "Strategy", 
                        Description = "Strategic thinking games", 
                        Cover = "strategy_cover.jpg",
                        IsActive = true 
                    },
                    new CategoryDto { 
                        Id = 4, 
                        CreatedAt = DateTime.Now.AddDays(-4), 
                        Name = "Sports", 
                        Description = "Sports simulation games", 
                        Cover = "sports_cover.jpg",
                        IsActive = true 
                    }
                };
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = mockCategories.Count,
                    data = mockCategories
                });
            }
        }

        /// <summary>
        /// Get all categories including inactive ones from the database
        /// </summary>
        /// <returns>List of all categories</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllCategories()
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Fetch all categories including inactive ones
                var response = await client
                    .From<Category>()
                    .Order(x => x.Name, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                // Convert BaseModel to DTO for serialization
                var categoryDtos = response.Models?.Select(x => x.ToDto()).ToList();
                
                return Ok(new { 
                    message = "All categories fetched from database", 
                    count = categoryDtos?.Count ?? 0,
                    data = categoryDtos
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                return BadRequest(new { 
                    message = "Error fetching categories", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get a specific category by ID
        /// </summary>
        /// <param name="id">The category ID</param>
        /// <returns>Category details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch specific category by ID using official Supabase client
                var response = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Get();
                
                var category = response.Models?.FirstOrDefault();
                
                if (category != null)
                {
                    return Ok(new { 
                        message = "Category found", 
                        data = category.ToDto()
                    });
                }
                
                return NotFound(new { 
                    message = $"Category with ID {id} not found" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching category", 
                    error = ex.Message 
                });
            }
        }
    }
}
