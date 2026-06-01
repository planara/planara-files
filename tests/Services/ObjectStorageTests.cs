using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Planara.Files.Interfaces;
using Planara.Files.Tests.Streams;

namespace Planara.Files.Tests.Services;

public class ObjectStorageTests : BaseApiTest
{
    public ObjectStorageTests(ApiTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UploadAsync_ValidObject_UploadsObject()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var bucket = CreateBucketName();
        var key = "models/cube.obj";

        await using var stream = CreateStream("o Cube");

        await storage.UploadAsync(
            bucket,
            key,
            stream,
            "text/plain");

        var downloaded = await storage.GetAsync(bucket, key);

        using var reader = new StreamReader(downloaded);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("o Cube");
    }

    [Fact]
    public async Task GetAsync_ExistingObject_ReturnsStream()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var bucket = CreateBucketName();
        var key = "images/test.png";

        await using var stream = CreateStream("image-content");

        await storage.UploadAsync(
            bucket,
            key,
            stream,
            "image/png");

        var downloaded = await storage.GetAsync(bucket, key);

        using var reader = new StreamReader(downloaded);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("image-content");
    }

    [Fact]
    public async Task DeleteAsync_ExistingObject_DeletesObject()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var bucket = CreateBucketName();
        var key = "images/delete.png";

        await using var stream = CreateStream("delete-me");

        await storage.UploadAsync(
            bucket,
            key,
            stream,
            "image/png");

        await storage.DeleteAsync(bucket, key);

        await storage.Invoking(x => x.GetAsync(bucket, key))
            .Should()
            .ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_InvalidBucket_ThrowsArgumentException(string bucket)
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        await using var stream = CreateStream("content");

        await storage.Invoking(x => x.UploadAsync(
                bucket,
                "key",
                stream,
                "text/plain"))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_InvalidKey_ThrowsArgumentException(string key)
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        await using var stream = CreateStream("content");

        await storage.Invoking(x => x.UploadAsync(
                CreateBucketName(),
                key,
                stream,
                "text/plain"))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_NullStream_ThrowsArgumentNullException()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        await storage.Invoking(x => x.UploadAsync(
                CreateBucketName(),
                "key",
                null!,
                "text/plain"))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }
    
    [Fact]
    public async Task UploadAsync_EmptyContentType_UsesOctetStream_AndUploadsObject()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var bucket = CreateBucketName();
        var key = "files/no-content-type.obj";

        await using var stream = CreateStream("Cube");

        await storage.UploadAsync(
            bucket,
            key,
            stream,
            "");

        await using var downloaded = await storage.GetAsync(bucket, key);

        using var reader = new StreamReader(downloaded);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("Cube");
    }
    
    [Fact]
    public async Task UploadAsync_NonSeekableStream_UploadsObject()
    {
        var storage = Scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var bucket = CreateBucketName();
        var key = "files/non-seekable.obj";

        await using var stream = new NonSeekableStream("NonSeekable");

        await storage.UploadAsync(
            bucket,
            key,
            stream,
            "text/plain");

        await using var downloaded = await storage.GetAsync(bucket, key);

        using var reader = new StreamReader(downloaded);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("NonSeekable");
    }

    private static string CreateBucketName()
        => $"test-bucket-{Guid.NewGuid():N}";

    private static MemoryStream CreateStream(string value)
        => new(System.Text.Encoding.UTF8.GetBytes(value));
}