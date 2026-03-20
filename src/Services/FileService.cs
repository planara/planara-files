using Microsoft.EntityFrameworkCore;
using Planara.Common.Exceptions;
using Planara.Files.Data;
using Planara.Files.Data.Domain;
using Planara.Files.Data.Enum;
using Planara.Files.Interfaces;

namespace Planara.Files.Services;

public class FileService(DataContext dbContext, IObjectStorage objectStorage, string defaultBucket = "files") : IFileService
{
    public async Task<FileMetadata> UploadAsync(
        Stream stream, 
        string originalFileName, 
        string contentType, 
        Guid ownerId,
        FileVisibility visibility = FileVisibility.Private, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("File name is required", nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/octet-stream";

        var extension = Path.GetExtension(originalFileName);
        var objectKey = GenerateObjectKey(extension);

        var metadata = new FileMetadata
        {
            UserId = ownerId,
            OriginalFileName = originalFileName,
            Extension = extension,
            ContentType = contentType,
            Size = GetStreamLengthSafe(stream),
            BucketName = defaultBucket,
            ObjectKey = objectKey,
            Visibility = visibility,
            Status = FileStatus.Uploading
        };

        await dbContext.FilesMetadata.AddAsync(metadata, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            if (stream.CanSeek)
                stream.Position = 0;

            await objectStorage.UploadAsync(metadata.BucketName, metadata.ObjectKey, stream, metadata.ContentType, cancellationToken);

            metadata.Status = FileStatus.Ready;
            await dbContext.SaveChangesAsync(cancellationToken);

            return metadata;
        }
        catch
        {
            metadata.Status = FileStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<FileMetadata?> GetMetadataAsync(Guid fileId, CancellationToken cancellationToken = default) => 
        await dbContext.FilesMetadata.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

    public async Task<Stream> DownloadAsync(Guid fileId, Guid userId, CancellationToken cancellationToken = default)
    {
        var metadata = await dbContext.FilesMetadata
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

        if (metadata is null)
            throw new NotFoundException();

        if (metadata.Status != FileStatus.Ready)
            throw new InvalidOperationException($"File '{fileId}' is not ready for download.");
        
        if (metadata.UserId != userId && metadata.Visibility == FileVisibility.Private)
            throw new UnauthorizedAccessException();

        return await objectStorage.GetAsync(metadata.BucketName, metadata.ObjectKey, cancellationToken);
    }

    public async Task DeleteAsync(Guid fileId, Guid userId, CancellationToken cancellationToken = default)
    {
        var metadata = await dbContext.FilesMetadata
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

        if (metadata is null)
            throw new NotFoundException();

        if (metadata.Status == FileStatus.Deleted)
            return;
        
        if (metadata.UserId != userId)
            throw new UnauthorizedAccessException();

        try
        {
            await objectStorage.DeleteAsync(metadata.BucketName, metadata.ObjectKey, cancellationToken);
        }
        finally
        {
            metadata.Status = FileStatus.Deleted;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<FileMetadata> SetVisibilityAsync(Guid fileId, FileVisibility visibility, CancellationToken cancellationToken = default)
    {
        var metadata = await dbContext.FilesMetadata
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

        if (metadata is null)
            throw new NotFoundException();

        metadata.Visibility = visibility;
        await dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    private static string GenerateObjectKey(string? extension)
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}{ext}";
    }

    private static long GetStreamLengthSafe(Stream stream) => stream.CanSeek ? stream.Length : 0;
}