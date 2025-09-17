using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Authorization;
using GameKeyStore.Models;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminCategoriesController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<AdminCategoriesController> _logger;

        public AdminCategoriesController(SupabaseService supabaseService, ILogger<AdminCategoriesController> logger)
        {
            _supabaseService = supabaseService;
            _logger = logger;
        }

        #region Categories Management

        /// <summary>
        /// Get all categories for admin management
        /// </summary>
        [HttpGet("categories")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> GetAllCategoriesAdmin()
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                var categoriesResponse = await client
                    .From<Category>()
                    .Get();

                var categories = categoriesResponse.Models?.Select(c => c.ToDto()) ?? new List<CategoryDto>();
                
                return Ok(new
                {
                    message = "Categories retrieved successfully",
                    data = categories,
                    count = categories.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving categories for admin");
                return StatusCode(500, new { message = "Error retrieving categories" });
            }
        }

        /// <summary>
        /// Create a new category
        /// </summary>
        [HttpPost("categories")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Category name is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if category name already exists
                // Get all categories and check for duplicates in C# since Supabase doesn't support ToLower() in LINQ
                var allCategoriesResponse = await client
                    .From<Category>()
                    .Get();
                
                var existingCategory = allCategoriesResponse.Models?
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x.Name) && 
                                   x.Name.Trim().Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                if (existingCategory != null)
                {
                    return BadRequest(new { message = "Category name already exists" });
                }

                var category = new Category
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Cover = request.Cover?.Trim(),
                    IsActive = request.IsActive ?? true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await client
                    .From<Category>()
                    .Insert(category);

                var createdCategory = result.Models?.FirstOrDefault();
                if (createdCategory != null)
                {
                    _logger.LogInformation("Category created successfully: {CategoryName} (ID: {CategoryId})", 
                        createdCategory.Name, createdCategory.Id);
                    
                    return CreatedAtAction(nameof(GetAllCategoriesAdmin), new { id = createdCategory.Id }, 
                        new { 
                            message = "Category created successfully", 
                            data = createdCategory.ToDto() 
                        });
                }

                return BadRequest(new { message = "Failed to create category" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category: {CategoryName}", request.Name);
                return StatusCode(500, new { message = "Error creating category" });
            }
        }

        /// <summary>
        /// Update an existing category
        /// </summary>
        [HttpPut("categories/{id}")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Category name is required" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if category exists
                var existingCategory = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingCategory == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Check if name already exists (excluding current category)
                // Get all categories and check for duplicates in C# since Supabase doesn't support ToLower() in LINQ
                var allCategoriesResponse = await client
                    .From<Category>()
                    .Get();
                
                var duplicateName = allCategoriesResponse.Models?
                    .FirstOrDefault(x => x.Id != id && 
                                   !string.IsNullOrEmpty(x.Name) && 
                                   x.Name.Trim().Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                if (duplicateName != null)
                {
                    return BadRequest(new { message = "Category name already exists" });
                }

                // Log the update request details for debugging
                _logger.LogInformation("Updating category {CategoryId}: Name='{Name}', Description='{Description}', Cover='{Cover}', IsActive={IsActive}", 
                    id, request.Name, request.Description, request.Cover, request.IsActive);

                // Update fields
                existingCategory.Name = request.Name.Trim();
                existingCategory.Description = request.Description?.Trim();
                existingCategory.Cover = request.Cover?.Trim();
                existingCategory.IsActive = request.IsActive ?? existingCategory.IsActive;

                _logger.LogInformation("About to update category in Supabase: {CategoryId}", id);

                var result = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Update(existingCategory);

                _logger.LogInformation("Supabase update result: {ResultCount} models returned", result.Models?.Count ?? 0);

                var updatedCategory = result.Models?.FirstOrDefault();
                if (updatedCategory != null)
                {
                    _logger.LogInformation("Category updated successfully: {CategoryName} (ID: {CategoryId})", 
                        updatedCategory.Name, updatedCategory.Id);
                    
                    return Ok(new { 
                        message = "Category updated successfully", 
                        data = updatedCategory.ToDto() 
                    });
                }

                _logger.LogWarning("Category update returned no models for ID: {CategoryId}", id);
                return BadRequest(new { message = "Failed to update category - no data returned from database" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category with ID: {CategoryId}. Exception type: {ExceptionType}, Message: {ExceptionMessage}", 
                    id, ex.GetType().Name, ex.Message);
                return StatusCode(500, new { 
                    message = "Error updating category",
                    error = ex.Message,
                    exceptionType = ex.GetType().Name
                });
            }
        }

        /// <summary>
        /// Delete a category (only if no games are using it)
        /// </summary>
        [HttpDelete("categories/{id}")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if category exists
                var existingCategory = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingCategory == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Check if any games are using this category
                var gamesUsingCategory = await client
                    .From<Game>()
                    .Where(x => x.CategoryId == id)
                    .Get();

                if (gamesUsingCategory.Models?.Any() == true)
                {
                    return BadRequest(new { 
                        message = "Cannot delete category because it is being used by games",
                        gamesCount = gamesUsingCategory.Models.Count()
                    });
                }

                // Delete category
                await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Delete();

                _logger.LogInformation("Category deleted successfully: {CategoryName} (ID: {CategoryId})", 
                    existingCategory.Name, existingCategory.Id);

                return Ok(new { 
                    message = "Category deleted successfully",
                    deletedCategory = existingCategory.ToDto()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category with ID: {CategoryId}", id);
                return StatusCode(500, new { message = "Error deleting category" });
            }
        }

        /// <summary>
        /// Toggle category active status
        /// </summary>
        [HttpPatch("categories/{id}/toggle-active")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> ToggleCategoryActive(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if category exists
                var existingCategory = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingCategory == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Toggle active status
                existingCategory.IsActive = !(existingCategory.IsActive ?? false);

                var result = await client
                    .From<Category>()
                    .Update(existingCategory);

                var updatedCategory = result.Models?.FirstOrDefault();
                if (updatedCategory != null)
                {
                    _logger.LogInformation("Category active status toggled: {CategoryName} (ID: {CategoryId}) -> {IsActive}", 
                        updatedCategory.Name, updatedCategory.Id, updatedCategory.IsActive);
                    
                    return Ok(new { 
                        message = $"Category {(updatedCategory.IsActive == true ? "activated" : "deactivated")} successfully", 
                        data = updatedCategory.ToDto() 
                    });
                }

                return BadRequest(new { message = "Failed to update category status" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling category active status with ID: {CategoryId}", id);
                return StatusCode(500, new { message = "Error updating category status" });
            }
        }

        /// <summary>
        /// Get category statistics (games count, etc.)
        /// </summary>
        [HttpGet("categories/{id}/stats")]
        [RequireCategoriesAdmin]
        public async Task<IActionResult> GetCategoryStats(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();

                // Check if category exists
                var existingCategory = await client
                    .From<Category>()
                    .Where(x => x.Id == id)
                    .Single();

                if (existingCategory == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Get games count
                var gamesResponse = await client
                    .From<Game>()
                    .Where(x => x.CategoryId == id)
                    .Get();

                var gamesCount = gamesResponse.Models?.Count() ?? 0;

                // Get total game keys count for this category
                var gameKeysQuery = @"
                    SELECT COUNT(*) as key_count
                    FROM game_keys gk
                    INNER JOIN games g ON gk.game_id = g.id
                    WHERE g.category_id = " + id;

                // For now, just return basic stats
                return Ok(new
                {
                    message = "Category statistics retrieved successfully",
                    data = new
                    {
                        category = existingCategory.ToDto(),
                        gamesCount = gamesCount,
                        isActive = existingCategory.IsActive ?? false
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category stats for ID: {CategoryId}", id);
                return StatusCode(500, new { message = "Error retrieving category statistics" });
            }
        }

        #endregion
    }

    #region Categories Request DTOs

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Cover { get; set; }
        public bool? IsActive { get; set; } = true;
    }

    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Cover { get; set; }
        public bool? IsActive { get; set; }
    }

    #endregion
}
