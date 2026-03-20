using Planara.Files.Data.Domain;
using Planara.Files.Data.Enum;

namespace Planara.Files.Interfaces;

public interface IFileService
{
    Task<FileMetadata> UploadAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        Guid ownerId,
        FileVisibility visibility = FileVisibility.Private,
        CancellationToken cancellationToken = default);

    Task<FileMetadata?> GetMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        Guid fileId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid fileId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<FileMetadata> SetVisibilityAsync(
        Guid fileId,
        FileVisibility visibility,
        CancellationToken cancellationToken = default);
}