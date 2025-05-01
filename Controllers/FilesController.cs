// Controllers/FilesController.cs
using FileSharingApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Security; // If not using FileService for content type

namespace FileSharingApi.Controllers
{
    [Route("api/[controller]")] // Route will be /api/files
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FilesController> _logger;

        // Inject the file service and logger
        public FilesController(IFileService fileService, ILogger<FilesController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        // POST /api/files
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        // Add request size limit attribute if needed (e.g., 100 MB)
        // [RequestSizeLimit(100 * 1024 * 1024)] // Example: 100 MB limit
        // [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)] // For multipart forms
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Upload attempt with no file.");
                return BadRequest("No file uploaded.");
            }

            // Optional: Add file size check
            // if (file.Length > YOUR_MAX_SIZE_IN_BYTES) {
            //     return BadRequest("File size exceeds limit.");
            // }

            // Optional: Add file type check (based on extension or magic bytes)
            // var allowedExtensions = new[] { ".jpg", ".png", ".pdf" };
            // var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            // if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext)) {
            //     return BadRequest("Invalid file type.");
            // }

            try
            {
                var savedFileName = await _fileService.SaveFileAsync(file);
                _logger.LogInformation("File {OriginalName} uploaded successfully as {SavedName}", file.FileName, savedFileName);

                // Return 201 Created status with the location of the new resource (optional but good practice)
                // The 'GetFile' action name must match the [HttpGet("{filename}")] action's name
                return CreatedAtAction(nameof(GetFile), new { filename = savedFileName }, new { fileName = savedFileName });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request during file upload: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (SecurityException ex)
            {
                _logger.LogError("Security exception during file upload: {Message}", ex.Message);
                return BadRequest("Invalid filename or path."); // Don't reveal too much detail
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error during file upload.");
                // Return a generic server error message to the client
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while saving the file. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/files
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListFiles()
        {
            var fileNames = await _fileService.GetFileNamesAsync();
            return Ok(fileNames);
        }

        // GET /api/files/{filename}
        [HttpGet("{filename}")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Should specify correct content type dynamically
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return BadRequest("Filename is required.");
            }

            // **Security Note:** Although the service sanitizes, basic validation here is good defense-in-depth.
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            {
                _logger.LogWarning("Potentially malicious filename detected in GET request: {Filename}", filename);
                return BadRequest("Invalid filename.");
            }

            var fileResult = await _fileService.GetFileAsync(filename);

            if (fileResult.FileStream == null || fileResult.ContentType == null)
            {
                _logger.LogWarning("File not found for download: {Filename}", filename);
                return NotFound($"File '{filename}' not found.");
            }

            _logger.LogInformation("Serving file {Filename} with content type {ContentType}", filename, fileResult.ContentType);

            // Return the file stream. The FileStreamResult will handle disposing the stream.
            // It also sets appropriate headers like Content-Disposition (inline by default)
            // and Content-Type.
            // To force download, add a third parameter: fileDownloadName: filename
            return File(fileResult.FileStream, fileResult.ContentType, fileDownloadName: filename);
        }

        // DELETE /api/files/{filename}
        [HttpDelete("{filename}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return BadRequest("Filename is required.");
            }

            // **Security Note:** Basic validation again.
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            {
                _logger.LogWarning("Potentially malicious filename detected in DELETE request: {Filename}", filename);
                return BadRequest("Invalid filename.");
            }

            var deleted = await _fileService.DeleteFileAsync(filename);

            if (!deleted)
            {
                _logger.LogWarning("File not found for deletion: {Filename}", filename);
                return NotFound($"File '{filename}' not found or could not be deleted.");
            }

            _logger.LogInformation("Successfully deleted file: {Filename}", filename);
            return NoContent(); // 204 No Content is standard for successful DELETE
        }
    }
}