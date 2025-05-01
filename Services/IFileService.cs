// Services/IFileService.cs
using Microsoft.AspNetCore.Mvc;

namespace FileSharingApi.Services
{
    public interface IFileService
    {
        /// <summary>
        /// Saves a file to the configured upload directory.
        /// </summary>
        /// <param name="file">The file to save.</param>
        /// <returns>The unique filename under which the file was saved.</returns>
        /// <exception cref="ArgumentException">Thrown if the file is invalid or empty.</exception>
        /// <exception cref="IOException">Thrown if a file system error occurs.</exception>
        Task<string> SaveFileAsync(IFormFile file);

        /// <summary>
        /// Retrieves a list of filenames currently stored.
        /// </summary>
        /// <returns>An enumerable collection of filenames.</returns>
        Task<IEnumerable<string>> GetFileNamesAsync();

        /// <summary>
        /// Gets the file stream and content type for a given filename.
        /// </summary>
        /// <param name="filename">The name of the file to retrieve.</param>
        /// <returns>A tuple containing the file stream and content type, or null if the file doesn't exist.</returns>
        Task<(Stream? FileStream, string? ContentType)> GetFileAsync(string filename);

        /// <summary>
        /// Deletes a file from the storage.
        /// </summary>
        /// <param name="filename">The name of the file to delete.</param>
        /// <returns>True if the file was successfully deleted, false if it didn't exist.</returns>
        Task<bool> DeleteFileAsync(string filename);
    }
}