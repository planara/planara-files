using Minio;
using Minio.DataModel.Args;
using Planara.Files.Interfaces;

namespace Planara.Files.Services;

public class ObjectStorage(IMinioClient minioClient) : IObjectStorage
{
    public async Task UploadAsync(string bucket, string key, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(stream);

        var bucketExists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken);

        if (!bucketExists)
            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);

        var size = stream.CanSeek ? stream.Length : -1;

        await minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType),
            cancellationToken);
    }

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var memory = new MemoryStream();

        await minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
                .WithCallbackStream(stream => stream.CopyTo(memory)),
            cancellationToken);

        memory.Position = 0;
        return memory;
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await minioClient.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(key),
            cancellationToken);
    }
}