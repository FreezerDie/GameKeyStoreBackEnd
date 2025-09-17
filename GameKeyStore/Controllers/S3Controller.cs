using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;
using System.Text.RegularExpressions;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class S3Controller : ControllerBase
    {
        private readonly S3Service _s3Service;
        private readonly ILogger<S3Controller> _logger;

        // Allowed file extensions for security
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg"
        };

        // Allowed content types
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", 
            "image/webp", "image/bmp", "image/svg+xml"
        };

        public S3Controller(S3Service s3Service, ILogger<S3Controller> logger)
        {
            _s3Service = s3Service;
            _logger = logger;
        }

        /// <summary>
        /// Generate a presigned URL for uploading a file to S3
        /// Uploaded files will be publicly accessible
        /// Requires games or categories write permission
        /// </summary>
        /// <param name="request">Upload request details</param>
        /// <returns>Presigned upload URL</returns>
        [HttpPost("presigned-upload-url")]
        [RequirePermission("s3", "presign")] // You can also use [RequireGamesWrite] or create a new permission for files
        public async Task<IActionResult> GeneratePresignedUploadUrl([FromBody] PresignedUploadUrlRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate file extension
                var extension = Path.GetExtension(request.FileName);
                if (!AllowedImageExtensions.Contains(extension))
                {
                    return BadRequest(new { 
                        message = $"File type not allowed. Allowed types: {string.Join(", ", AllowedImageExtensions)}" 
                    });
                }

                // Validate content type
                if (!AllowedContentTypes.Contains(request.ContentType))
                {
                    return BadRequest(new { 
                        message = $"Content type not allowed. Allowed types: {string.Join(", ", AllowedContentTypes)}" 
                    });
                }

                // Sanitize filename
                var sanitizedFileName = SanitizeFileName(request.FileName);
                
                // Add prefix if provided
                var fullPath = string.IsNullOrEmpty(request.Prefix) 
                    ? sanitizedFileName 
                    : $"{request.Prefix.TrimEnd('/')}/{sanitizedFileName}";

                // Generate unique filename to prevent conflicts
                var uniqueFileName = GenerateUniqueFileName(fullPath);

                // Generate presigned URL
                var uploadUrl = await _s3Service.GeneratePresignedUploadUrlAsync(
                    uniqueFileName, 
                    request.ContentType, 
                    request.ExpirationMinutes);

                var response = new PresignedUploadUrlResponseDto
                {
                    UploadUrl = uploadUrl,
                    FileName = Path.GetFileName(uniqueFileName),
                    FullPath = uniqueFileName,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes),
                    Message = "Presigned upload URL generated successfully. File will be publicly accessible after upload."
                };

                _logger.LogInformation("Generated presigned upload URL for file: {FileName}", uniqueFileName);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned upload URL");
                return StatusCode(500, new { message = "Internal server error while generating upload URL" });
            }
        }

        /// <summary>
        /// Delete a file from S3 bucket
        /// Requires games or categories admin permission
        /// </summary>
        /// <param name="request">Delete request details</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("delete-file")]
        [RequirePermission("s3", "delete")] // You can also use [RequireGamesAdmin] or create a new permission for files
        public async Task<IActionResult> DeleteFile([FromBody] DeleteFileRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if file exists first
                var fileExists = await _s3Service.FileExistsAsync(request.FileName);
                if (!fileExists)
                {
                    return NotFound(new DeleteFileResponseDto
                    {
                        Success = false,
                        FileName = request.FileName,
                        Message = "File not found"
                    });
                }

                // Delete the file
                var deleteSuccess = await _s3Service.DeleteFileAsync(request.FileName);

                var response = new DeleteFileResponseDto
                {
                    Success = deleteSuccess,
                    FileName = request.FileName,
                    Message = deleteSuccess ? "File deleted successfully" : "Failed to delete file"
                };

                if (deleteSuccess)
                {
                    _logger.LogInformation("Successfully deleted file: {FileName}", request.FileName);
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning("Failed to delete file: {FileName}", request.FileName);
                    return StatusCode(500, response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileName}", request.FileName);
                return StatusCode(500, new DeleteFileResponseDto
                {
                    Success = false,
                    FileName = request.FileName,
                    Message = "Internal server error while deleting file"
                });
            }
        }

        /// <summary>
        /// Generate a presigned URL for downloading/viewing a file
        /// Public endpoint for viewing images
        /// </summary>
        /// <param name="request">Download request details</param>
        /// <returns>Presigned download URL</returns>
        [HttpPost("presigned-download-url")]
        public async Task<IActionResult> GeneratePresignedDownloadUrl([FromBody] PresignedDownloadUrlRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if file exists
                var fileExists = await _s3Service.FileExistsAsync(request.FileName);
                if (!fileExists)
                {
                    return NotFound(new { message = "File not found" });
                }

                // Generate presigned URL
                var downloadUrl = await _s3Service.GeneratePresignedDownloadUrlAsync(
                    request.FileName, 
                    request.ExpirationMinutes);

                var response = new PresignedDownloadUrlResponseDto
                {
                    DownloadUrl = downloadUrl,
                    FileName = request.FileName,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes),
                    Message = "Presigned download URL generated successfully"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned download URL for file: {FileName}", request.FileName);
                return StatusCode(500, new { message = "Internal server error while generating download URL" });
            }
        }

        /// <summary>
        /// Check if a file exists in the S3 bucket
        /// </summary>
        /// <param name="fileName">Name of the file to check</param>
        /// <returns>File existence status</returns>
        [HttpGet("file-exists")]
        public async Task<IActionResult> CheckFileExists([FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest(new { message = "Filename is required" });
                }

                var exists = await _s3Service.FileExistsAsync(fileName);
                return Ok(new { exists, fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence: {FileName}", fileName);
                return StatusCode(500, new { message = "Internal server error while checking file existence" });
            }
        }

        /// <summary>
        /// Sanitize filename to prevent security issues
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Replace spaces with underscores
            sanitized = sanitized.Replace(" ", "_");
            
            // Remove any remaining special characters except dots, underscores, and hyphens
            sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9._-]", "");
            
            return sanitized;
        }

        /// <summary>
        /// Generate a unique filename using UUID
        /// </summary>
        private static string GenerateUniqueFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var directory = Path.GetDirectoryName(originalFileName);
            
            // Generate UUID for completely unique filename
            var uuid = Guid.NewGuid().ToString();
            var uniqueName = $"{uuid}{extension}";
            
            return string.IsNullOrEmpty(directory) 
                ? uniqueName 
                : Path.Combine(directory, uniqueName).Replace("\\", "/");
        }
    }
}
