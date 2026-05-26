using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planara.Common.Auth.Claims;
using Planara.Files.Interfaces;

namespace Planara.Files.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(IFileService fileService) : ControllerBase
{
    [Authorize]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1024 * 1024 * 100)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (!IsAllowedFile(file))
            return BadRequest("Unsupported file type. Allowed types: png, jpeg, webp, obj.");

        var userId = User.GetUserId();

        await using var stream = file.OpenReadStream();

        var metadata = await fileService.UploadAsync(
            stream,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            userId,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            metadata.Id,
            metadata.OriginalFileName,
            metadata.ContentType,
            metadata.Size,
            metadata.Visibility,
            metadata.Status
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var metadata = await fileService.GetMetadataAsync(id, cancellationToken);
        
        if (metadata is null)
            return NotFound();
        
        var userId = User.GetUserId();

        var stream = await fileService.DownloadAsync(id, userId, cancellationToken);

        return File(stream, metadata.ContentType, metadata.OriginalFileName);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        
        await fileService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
    
    [Authorize]
    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1024 * 1024 * 100)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (!IsAllowedFile(file))
            return BadRequest("Unsupported file type. Allowed types: png, jpeg, webp, obj.");

        var userId = User.GetUserId();

        await using var stream = file.OpenReadStream();

        var metadata = await fileService.UpdateAsync(
            id,
            stream,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            userId,
            cancellationToken);

        return Ok(new
        {
            metadata.Id,
            metadata.OriginalFileName,
            metadata.ContentType,
            metadata.Size,
            metadata.Visibility,
            metadata.Status
        });
    }
    
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".obj"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        
        "model/obj",
        "application/octet-stream",
        "text/plain"
    };

    private static bool IsAllowedFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension))
            return false;

        if (!AllowedExtensions.Contains(extension))
            return false;

        if (string.IsNullOrWhiteSpace(file.ContentType))
            return extension.Equals(".obj", StringComparison.OrdinalIgnoreCase);

        if (extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType.Equals("model/obj", StringComparison.OrdinalIgnoreCase)
                   || file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                   || file.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
        }

        return AllowedContentTypes.Contains(file.ContentType);
    }
}