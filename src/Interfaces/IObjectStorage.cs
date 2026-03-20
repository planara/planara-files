namespace Planara.Files.Interfaces;

public interface IObjectStorage
{
    Task UploadAsync(string bucket, string key, Stream stream, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string bucket, string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default);
}