using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;

        public RolesController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// Get all roles from the database
        /// </summary>
        /// <returns>List of roles</returns>
        [HttpGet]
        [RequireRolesRead]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                // Initialize Supabase connection
                await _supabaseService.InitializeAsync();
                
                var client = _supabaseService.GetClient();
                
                // Fetch data from the roles table using official Supabase client
                var response = await client
                    .From<Role>()
                    .Order(x => x.Name!, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                // Convert BaseModel to DTO for serialization
                var roleDtos = response.Models?.Select(x => x.ToDto()).ToList();
                
                return Ok(new { 
                    message = "Roles fetched from database", 
                    count = roleDtos?.Count ?? 0,
                    data = roleDtos
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database error: {ex.Message}");
                
                // Return mock data as fallback
                var mockRoles = new List<RoleDto>
                {
                    new RoleDto { Id = 1, Name = "admin" },
                    new RoleDto { Id = 2, Name = "user" },
                    new RoleDto { Id = 3, Name = "moderator" }
                };
                
                return Ok(new { 
                    message = "Using fallback data - database connection failed", 
                    error = ex.Message,
                    count = mockRoles.Count,
                    data = mockRoles
                });
            }
        }

        /// <summary>
        /// Get a specific role by ID
        /// </summary>
        /// <param name="id">The role ID</param>
        /// <returns>Role details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(long id)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch specific role by ID using official Supabase client
                var response = await client
                    .From<Role>()
                    .Where(x => x.Id == id)
                    .Get();
                
                var role = response.Models?.FirstOrDefault();
                
                if (role != null)
                {
                    return Ok(new { 
                        message = "Role found", 
                        data = role.ToDto()
                    });
                }
                
                return NotFound(new { 
                    message = $"Role with ID {id} not found" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching role", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get a role by name
        /// </summary>
        /// <param name="name">The role name</param>
        /// <returns>Role details</returns>
        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetRoleByName(string name)
        {
            try
            {
                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch role by name (get all and filter in C# due to Supabase LINQ limitations)
                var response = await client
                    .From<Role>()
                    .Get();
                
                var role = response.Models?
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x.Name) && 
                                   x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                
                if (role != null)
                {
                    return Ok(new { 
                        message = "Role found", 
                        data = role.ToDto()
                    });
                }
                
                return NotFound(new { 
                    message = $"Role with name '{name}' not found" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error fetching role", 
                    error = ex.Message 
                });
            }
        }
    }
}
