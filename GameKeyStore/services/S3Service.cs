using Amazon.S3;
using Amazon.S3.Model;
using Amazon;

namespace GameKeyStore.Services
{
    public class S3Service
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly ILogger<S3Service> _logger;

        public S3Service(ILogger<S3Service> logger)
        {
            _logger = logger;
            
            // Load configuration from environment variables
            var accessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY") 
                           ?? throw new InvalidOperationException("S3_ACCESS_KEY environment variable is not set");
            var secretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY") 
                           ?? throw new InvalidOperationException("S3_SECRET_KEY environment variable is not set");
            var serviceUrl = Environment.GetEnvironmentVariable("S3_SERVICE_URL") 
                           ?? throw new InvalidOperationException("S3_SERVICE_URL environment variable is not set");
            
            _bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME") 
                         ?? throw new InvalidOperationException("S3_BUCKET_NAME environment variable is not set");

            // Configure S3 client for tebi.io
            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true, // Required for S3-compatible services
                UseHttp = serviceUrl.StartsWith("http://"), // Use HTTP if service URL specifies it
                SignatureVersion = "4"
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }

        /// <summary>
        /// Generate a presigned URL for uploading a file to S3
        /// </summary>
        /// <param name="fileName">The name of the file to upload</param>
        /// <param name="contentType">The MIME type of the file</param>
        /// <param name="expirationMinutes">How long the URL should be valid (default: 15 minutes)</param>
        /// <returns>Presigned URL for upload</returns>
        public async Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, int expirationMinutes = 15)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    Verb = HttpVerb.PUT,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    ContentType = contentType
                };

                // Add ACL parameter to make uploaded files publicly accessible
                request.Parameters.Add("x-amz-acl", "public-read");

                var url = await _s3Client.GetPreSignedURLAsync(request);
                _logger.LogInformation("Generated presigned upload URL for file: {FileName} with public read access", fileName);
                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned upload URL for file: {FileName}", fileName);
                throw;
            }
        }

        /// <summary>
        /// Delete a file from S3 bucket
        /// </summary>
        /// <param name="fileName">The name of the file to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName
                };

                var response = await _s3Client.DeleteObjectAsync(request);
                _logger.LogInformation("Successfully deleted file: {FileName}", fileName);
                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || 
                       response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileName}", fileName);
                return false;
            }
        }

        /// <summary>
        /// Check if a file exists in the S3 bucket
        /// </summary>
        /// <param name="fileName">The name of the file to check</param>
        /// <returns>True if file exists, false otherwise</returns>
        public async Task<bool> FileExistsAsync(string fileName)
        {
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = fileName
                };

                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file exists: {FileName}", fileName);
                return false;
            }
        }

        /// <summary>
        /// Generate a presigned URL for downloading/viewing a file
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="expirationMinutes">How long the URL should be valid (default: 60 minutes)</param>
        /// <returns>Presigned URL for download</returns>
        public async Task<string> GeneratePresignedDownloadUrlAsync(string fileName, int expirationMinutes = 60)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
                };

                var url = await _s3Client.GetPreSignedURLAsync(request);
                _logger.LogInformation("Generated presigned download URL for file: {FileName}", fileName);
                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned download URL for file: {FileName}", fileName);
                throw;
            }
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}