using System.Security.Claims;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planara.Common.Auth.Claims;
using Planara.Files.Interfaces;

namespace Planara.Files.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(IFileService fileService) : ControllerBase
{
    [Authorize]
    [HttpPost("upload")]
    [RequestSizeLimit(1024 * 1024 * 100)]
    public async Task<IActionResult> Upload(IFormFile file, ClaimsPrincipal claims, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var userId = claims.GetUserId();

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
    public async Task<IActionResult> Download(Guid id, ClaimsPrincipal claims, CancellationToken cancellationToken)
    {
        var metadata = await fileService.GetMetadataAsync(id, cancellationToken);
        
        if (metadata is null)
            return NotFound();
        
        var userId = claims.GetUserId();

        var stream = await fileService.DownloadAsync(id, userId, cancellationToken);

        return File(stream, metadata.ContentType, metadata.OriginalFileName);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, ClaimsPrincipal claims, CancellationToken cancellationToken)
    {
        var userId = claims.GetUserId();
        
        await fileService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}