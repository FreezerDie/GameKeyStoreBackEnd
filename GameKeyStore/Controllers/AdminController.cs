using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Constants;
using GameKeyStore.Authorization;
using GameKeyStore.Models;

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
        /// Seed all default permissions
        /// </summary>
        [HttpPost("seed-permissions")]
        [RequireRolesAdmin]
        public async Task<IActionResult> SeedPermissions()
        {
            try
            {
                var success = await _permissionManager.SeedPermissionsAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Permissions seeded successfully"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        message = "Failed to seed permissions"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding permissions");
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
                    description = t.Description,
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
                    request.Description ?? "", 
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

                // Get counts from database
                var permissionsResponse = await client.From<Permission>().Get();
                var rolesResponse = await client.From<Role>().Get();
                var usersResponse = await client.From<User>().Get();

                var permissionsCount = permissionsResponse.Models?.Count ?? 0;
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

    public class UpdateRolePermissionsRequest
    {
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }

    public class ValidatePermissionsRequest
    {
        public string[] PermissionNames { get; set; } = Array.Empty<string>();
    }
}
