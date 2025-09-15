using System.ComponentModel.DataAnnotations;

namespace GameKeyStore.Models
{
    /// <summary>
    /// Request model for generating presigned upload URL
    /// </summary>
    public class PresignedUploadUrlRequestDto
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string ContentType { get; set; } = string.Empty;

        [Range(1, 60)]
        public int ExpirationMinutes { get; set; } = 15;

        /// <summary>
        /// Optional prefix for organizing files (e.g., "games/covers/", "categories/")
        /// </summary>
        [StringLength(100)]
        public string? Prefix { get; set; }
    }

    /// <summary>
    /// Response model for presigned upload URL
    /// </summary>
    public class PresignedUploadUrlResponseDto
    {
        public string UploadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for deleting a file
    /// </summary>
    public class DeleteFileRequestDto
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for file deletion
    /// </summary>
    public class DeleteFileResponseDto
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for generating presigned download URL
    /// </summary>
    public class PresignedDownloadUrlRequestDto
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string FileName { get; set; } = string.Empty;

        [Range(1, 1440)] // Max 24 hours
        public int ExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// Response model for presigned download URL
    /// </summary>
    public class PresignedDownloadUrlResponseDto
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
