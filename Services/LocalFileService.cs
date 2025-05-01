// Services/LocalFileService.cs
using Microsoft.Extensions.Options;
using System.IO;
using Microsoft.AspNetCore.StaticFiles;
using System.Security; // For content type detection

namespace FileSharingApi.Services
{
    // Simple configuration class (optional but good practice)
    public class StorageSettings
    {
        public string UploadsFolderPath { get; set; } = "Uploads"; // Default value
    }

    public class LocalFileService : IFileService
    {
        private readonly string _uploadsFolderPath;
        private readonly ILogger<LocalFileService> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        // Inject IOptions<StorageSettings> to get configured path and ILogger for logging
        public LocalFileService(IOptions<StorageSettings> storageSettings, ILogger<LocalFileService> logger)
        {
            _uploadsFolderPath = Path.GetFullPath(storageSettings.Value.UploadsFolderPath ?? "Uploads"); // Ensure it's an absolute path
            _logger = logger;
            _contentTypeProvider = new FileExtensionContentTypeProvider(); // Used to determine MIME type

            // Ensure the upload directory exists
            if (!Directory.Exists(_uploadsFolderPath))
            {
                Directory.CreateDirectory(_uploadsFolderPath);
                _logger.LogInformation("Created upload directory at: {Path}", _uploadsFolderPath);
            }
            _logger.LogInformation("Using uploads directory: {Path}", _uploadsFolderPath);
        }

        public async Task<string> SaveFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File cannot be null or empty.", nameof(file));
            }

            // **Security Note:** Sanitize the filename provided by the client.
            // Here, we use the original filename but remove path characters.
            // For more robustness, consider generating a unique filename (e.g., using Guid.NewGuid()).
            var originalFileName = Path.GetFileName(file.FileName); // Extracts only the filename part, removing any path info
            var safeFileName = SanitizeFileName(originalFileName);

            // **Security Note:** Ensure the final path is truly within the intended uploads folder.
            var filePath = Path.Combine(_uploadsFolderPath, safeFileName);

            // Prevent path traversal attacks (though Path.Combine and GetFileName help significantly)
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_uploadsFolderPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException("Invalid file path detected.");
            }

            // Handle potential filename collisions (optional: add logic to rename or reject)
            // if (File.Exists(filePath)) {
            //    // Example: Append a timestamp or number
            //    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            //    safeFileName = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{timestamp}{Path.GetExtension(safeFileName)}";
            //    filePath = Path.Combine(_uploadsFolderPath, safeFileName);
            // }

            _logger.LogInformation("Attempting to save file to: {Path}", filePath);

            try
            {
                // Use a 'using' statement to ensure the stream is disposed correctly
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                _logger.LogInformation("Successfully saved file: {FileName} to path: {Path}", safeFileName, filePath);
                return safeFileName; // Return the name it was saved under
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file {FileName}", safeFileName);
                // Clean up partially created file if error occurs
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); }
                    catch (Exception deleteEx) { _logger.LogError(deleteEx, "Error cleaning up file after save failure: {Path}", filePath); }
                }
                throw new IOException($"An error occurred while saving the file: {ex.Message}", ex);
            }
        }

        public Task<IEnumerable<string>> GetFileNamesAsync()
        {
            try
            {
                var fileNames = Directory.EnumerateFiles(_uploadsFolderPath)
                                         .Select(Path.GetFileName)!; // Select only the filename part
                return Task.FromResult(fileNames);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex, "Upload directory not found when listing files: {Path}", _uploadsFolderPath);
                return Task.FromResult(Enumerable.Empty<string>()); // Return empty list if dir doesn't exist
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files in directory: {Path}", _uploadsFolderPath);
                throw; // Re-throw other unexpected errors
            }
        }

        public Task<(Stream?, string?)> GetFileAsync(string filename)
        {
            // **Security Note:** Sanitize the input filename again before using it.
            var safeFileName = SanitizeFileName(filename);
            var filePath = Path.Combine(_uploadsFolderPath, safeFileName);

            // **Security Note:** Double-check it's within the uploads folder boundary.
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_uploadsFolderPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempt to access file outside upload directory denied: {Filename}", filename);
                return Task.FromResult<(Stream?, string?)>((null, null)); // Or throw an exception
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Requested file not found: {Path}", filePath);
                return Task.FromResult<(Stream?, string?)>((null, null));
            }

            // Try to determine the content type (MIME type)
            if (!_contentTypeProvider.TryGetContentType(safeFileName, out var contentType))
            {
                contentType = "application/octet-stream"; // Default fallback
                _logger.LogDebug("Could not determine content type for {FileName}, using default.", safeFileName);
            }

            // IMPORTANT: The stream will be disposed by the Controller's FileStreamResult
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            _logger.LogInformation("Providing stream for file: {FileName} with content type {ContentType}", safeFileName, contentType);

            return Task.FromResult<(Stream?, string?)>((fileStream, contentType));
        }

        public Task<bool> DeleteFileAsync(string filename)
        {
            // **Security Note:** Sanitize the input filename.
            var safeFileName = SanitizeFileName(filename);
            var filePath = Path.Combine(_uploadsFolderPath, safeFileName);

            // **Security Note:** Double-check it's within the uploads folder boundary.
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_uploadsFolderPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempt to delete file outside upload directory denied: {Filename}", filename);
                return Task.FromResult(false);
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Successfully deleted file: {Path}", filePath);
                    return Task.FromResult(true);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete non-existent file: {Path}", filePath);
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {Path}", filePath);
                // Depending on requirements, you might want to re-throw or just return false
                return Task.FromResult(false);
            }
        }

        // Basic sanitization helper
        private string SanitizeFileName(string fileName)
        {
            // Remove path characters and potentially harmful sequences
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());

            // Replace potentially problematic sequences like ".."
            sanitized = sanitized.Replace("..", "_");

            // You might want to add more rules, e.g., limit length, enforce extensions
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                // Generate a default name if sanitization results in empty string
                sanitized = $"unnamed_file_{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Trim leading/trailing whitespace/dots which can cause issues on some filesystems
            sanitized = sanitized.Trim('.', ' ');

            return sanitized;
        }
    }
}