using Planara.Files.Interfaces;

namespace Planara.Files.Tests.Fakes;

public class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<(string Bucket, string Key), byte[]> _objects = [];

    public List<UploadCall> UploadCalls { get; } = [];
    public List<GetCall> GetCalls { get; } = [];
    public List<DeleteCall> DeleteCalls { get; } = [];

    public bool ThrowOnUpload { get; set; }
    public bool ThrowOnGet { get; set; }
    public bool ThrowOnDelete { get; set; }

    public Exception UploadException { get; set; } = new InvalidOperationException("Upload failed");
    public Exception GetException { get; set; } = new InvalidOperationException("Get failed");
    public Exception DeleteException { get; set; } = new InvalidOperationException("Delete failed");

    public async Task UploadAsync(
        string bucket,
        string key,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        UploadCalls.Add(new UploadCall(bucket, key, contentType));

        if (ThrowOnUpload)
            throw UploadException;

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        _objects[(bucket, key)] = memory.ToArray();
    }

    public Task<Stream> GetAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        GetCalls.Add(new GetCall(bucket, key));

        if (ThrowOnGet)
            throw GetException;

        if (!_objects.TryGetValue((bucket, key), out var bytes))
            throw new FileNotFoundException("Object not found", key);

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(new DeleteCall(bucket, key));

        if (ThrowOnDelete)
            throw DeleteException;

        _objects.Remove((bucket, key));

        return Task.CompletedTask;
    }

    public sealed record UploadCall(string Bucket, string Key, string ContentType);

    public sealed record GetCall(string Bucket, string Key);

    public sealed record DeleteCall(string Bucket, string Key);
}