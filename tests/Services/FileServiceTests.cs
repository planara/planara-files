using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Planara.Common.Exceptions;
using Planara.Files.Data.Domain;
using Planara.Files.Data.Enum;
using Planara.Files.Interfaces;
using Planara.Files.Services;
using Planara.Files.Tests.Fakes;
using Planara.Files.Tests.Streams;

namespace Planara.Files.Tests.Services;

public class FileServiceTests : BaseApiTest
{
    public FileServiceTests(ApiTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UploadAsync_ValidFile_SavesMetadata_UploadsObject_AndMarksReady()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        var payload = "test";
        await using var stream = CreateStream(payload);

        var metadata = await service.UploadAsync(
            stream,
            "image.png",
            "image/png",
            UserId);

        metadata.Id.Should().NotBeEmpty();
        metadata.UserId.Should().Be(UserId);
        metadata.OriginalFileName.Should().Be("image.png");
        metadata.Extension.Should().Be(".png");
        metadata.ContentType.Should().Be("image/png");
        metadata.Size.Should().Be(System.Text.Encoding.UTF8.GetByteCount(payload));
        metadata.Visibility.Should().Be(FileVisibility.Private);
        metadata.Status.Should().Be(FileStatus.Ready);
        metadata.BucketName.Should().Be("files");
        metadata.ObjectKey.Should().EndWith(".png");

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.Status.Should().Be(FileStatus.Ready);

        await using var downloaded = await service.DownloadAsync(metadata.Id, UserId);
        using var reader = new StreamReader(downloaded);

        var content = await reader.ReadToEndAsync();

        content.Should().Be("test");
    }

    [Fact]
    public async Task UploadAsync_EmptyContentType_UsesOctetStream()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        var metadata = await service.UploadAsync(
            stream,
            "model.obj",
            "",
            UserId);

        metadata.ContentType.Should().Be("application/octet-stream");
        metadata.Status.Should().Be(FileStatus.Ready);
    }

    [Fact]
    public async Task UploadAsync_NullStream_ThrowsArgumentNullException()
    {
        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.UploadAsync(
                null!,
                "image.png",
                "image/png",
                UserId))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_EmptyFileName_ThrowsArgumentException(string fileName)
    {
        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UploadAsync(
                stream,
                fileName,
                "image/png",
                UserId))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetMetadataAsync_ExistingFile_ReturnsMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        var result = await service.GetMetadataAsync(metadata.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(metadata.Id);
    }

    [Fact]
    public async Task GetMetadataAsync_MissingFile_ReturnsNull()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        var result = await service.GetMetadataAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_MissingFile_ThrowsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.DownloadAsync(Guid.NewGuid(), UserId))
            .Should()
            .ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DownloadAsync_NotReadyFile_ThrowsInvalidOperationException()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Uploading,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.DownloadAsync(metadata.Id, UserId))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DownloadAsync_PrivateFileOfAnotherUser_ThrowsUnauthorized()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: Guid.NewGuid(),
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.DownloadAsync(metadata.Id, UserId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DownloadAsync_PublicFileOfAnotherUser_ReturnsStream()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var ownerId = Guid.NewGuid();

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var uploadStream = CreateStream("public-file");

        var metadata = await service.UploadAsync(
            uploadStream,
            "public.obj",
            "text/plain",
            ownerId,
            FileVisibility.Public);

        await using var downloaded = await service.DownloadAsync(metadata.Id, UserId);
        using var reader = new StreamReader(downloaded);

        var content = await reader.ReadToEndAsync();

        content.Should().Be("public-file");
    }

    [Fact]
    public async Task DeleteAsync_OwnFile_DeletesObject_AndMarksDeleted()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        var metadata = await service.UploadAsync(
            stream,
            "image.png",
            "image/png",
            UserId);

        await service.DeleteAsync(metadata.Id, UserId);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.Status.Should().Be(FileStatus.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_ThrowsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.DeleteAsync(Guid.NewGuid(), UserId))
            .Should()
            .ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeletedFile_DoesNothing()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Deleted,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.DeleteAsync(metadata.Id, UserId);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.Status.Should().Be(FileStatus.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_FileOfAnotherUser_ThrowsUnauthorized()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: Guid.NewGuid(),
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.DeleteAsync(metadata.Id, UserId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetVisibilityAsync_ExistingFile_UpdatesVisibility()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        var updated = await service.SetVisibilityAsync(
            metadata.Id,
            FileVisibility.Public);

        updated.Visibility.Should().Be(FileVisibility.Public);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.Visibility.Should().Be(FileVisibility.Public);
    }

    [Fact]
    public async Task SetVisibilityAsync_MissingFile_ThrowsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await service.Invoking(x => x.SetVisibilityAsync(
                Guid.NewGuid(),
                FileVisibility.Public))
            .Should()
            .ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_OwnFile_UploadsNewObject_UpdatesMetadata_AndOldFileIsNoLongerDownloadable()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var oldStream = CreateStream("old-file");

        var metadata = await service.UploadAsync(
            oldStream,
            "old.obj",
            "text/plain",
            UserId);

        var oldObjectKey = metadata.ObjectKey;

        await using var newStream = CreateStream("test");

        var updated = await service.UpdateAsync(
            metadata.Id,
            newStream,
            "new.webp",
            "image/webp",
            UserId);

        updated.Id.Should().Be(metadata.Id);
        updated.OriginalFileName.Should().Be("new.webp");
        updated.Extension.Should().Be(".webp");
        updated.ContentType.Should().Be("image/webp");
        updated.Size.Should().Be(4);
        updated.Status.Should().Be(FileStatus.Ready);
        updated.ObjectKey.Should().NotBe(oldObjectKey);

        await using var downloaded = await service.DownloadAsync(metadata.Id, UserId);
        using var reader = new StreamReader(downloaded);

        var content = await reader.ReadToEndAsync();

        content.Should().Be("test");
    }

    [Fact]
    public async Task UpdateAsync_MissingFile_ThrowsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UpdateAsync(
                Guid.NewGuid(),
                stream,
                "new.webp",
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_DeletedFile_ThrowsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Deleted,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UpdateAsync(
                metadata.Id,
                stream,
                "new.webp",
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_FileOfAnotherUser_ThrowsUnauthorized()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: Guid.NewGuid(),
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UpdateAsync(
                metadata.Id,
                stream,
                "new.webp",
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_EmptyFileName_ThrowsArgumentException(string fileName)
    {
        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UpdateAsync(
                Guid.NewGuid(),
                stream,
                fileName,
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_EmptyContentType_UsesOctetStream()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var service = Scope.ServiceProvider.GetRequiredService<IFileService>();

        await using var oldStream = CreateStream("old-file");

        var metadata = await service.UploadAsync(
            oldStream,
            "old.obj",
            "text/plain",
            UserId);

        await using var newStream = CreateStream("test");

        var updated = await service.UpdateAsync(
            metadata.Id,
            newStream,
            "new.obj",
            "",
            UserId);

        updated.ContentType.Should().Be("application/octet-stream");
    }
    
    [Fact]
    public async Task UploadAsync_WhenStorageUploadFails_MarksMetadataAsFailed_AndThrows()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var storage = new FakeObjectStorage
        {
            ThrowOnUpload = true
        };

        var service = CreateService(storage);

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UploadAsync(
                stream,
                "image.png",
                "image/png",
                UserId))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        metadata.Status.Should().Be(FileStatus.Failed);
        metadata.OriginalFileName.Should().Be("image.png");
        metadata.ContentType.Should().Be("image/png");

        storage.UploadCalls.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task UploadAsync_NonSeekableStreamWithoutExtension_SavesSizeAsZero_AndObjectKeyWithoutExtension()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var storage = new FakeObjectStorage();
        var service = CreateService(storage);

        await using var stream = new NonSeekableStream("non-seekable-content");

        var metadata = await service.UploadAsync(
            stream,
            "README",
            "text/plain",
            UserId);

        metadata.Size.Should().Be(0);
        metadata.Extension.Should().BeEmpty();
        metadata.ObjectKey.Should().NotEndWith(".");
        metadata.Status.Should().Be(FileStatus.Ready);

        storage.UploadCalls.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task UpdateAsync_WhenNewUploadFails_KeepsOldMetadata_DeletesNewObject_AndThrows()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var oldObjectKey = metadata.ObjectKey;
        var oldFileName = metadata.OriginalFileName;
        var oldContentType = metadata.ContentType;

        var storage = new FakeObjectStorage
        {
            ThrowOnUpload = true
        };

        var service = CreateService(storage);

        await using var stream = CreateStream("test");

        await service.Invoking(x => x.UpdateAsync(
                metadata.Id,
                stream,
                "new.webp",
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.ObjectKey.Should().Be(oldObjectKey);
        saved.OriginalFileName.Should().Be(oldFileName);
        saved.ContentType.Should().Be(oldContentType);
        saved.Status.Should().Be(FileStatus.Ready);

        storage.UploadCalls.Should().HaveCount(1);
        storage.DeleteCalls.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task UpdateAsync_WhenNewUploadFails_AndCleanupFails_KeepsOldMetadata_AndThrowsOriginalError()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var oldObjectKey = metadata.ObjectKey;
        var oldFileName = metadata.OriginalFileName;

        var uploadException = new InvalidOperationException("upload error");

        var storage = new FakeObjectStorage
        {
            ThrowOnUpload = true,
            ThrowOnDelete = true,
            UploadException = uploadException
        };

        var service = CreateService(storage);

        await using var stream = CreateStream("test");

        var exception = await service.Invoking(x => x.UpdateAsync(
                metadata.Id,
                stream,
                "new.webp",
                "image/webp",
                UserId))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Be("upload error");

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.ObjectKey.Should().Be(oldObjectKey);
        saved.OriginalFileName.Should().Be(oldFileName);

        storage.UploadCalls.Should().HaveCount(1);
        storage.DeleteCalls.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task UpdateAsync_WhenOldObjectDeleteFails_ReturnsUpdatedMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var oldObjectKey = metadata.ObjectKey;

        var storage = new FakeObjectStorage
        {
            ThrowOnDelete = true
        };

        var service = CreateService(storage);

        await using var stream = CreateStream("test");

        var updated = await service.UpdateAsync(
            metadata.Id,
            stream,
            "new.webp",
            "image/webp",
            UserId);

        updated.OriginalFileName.Should().Be("new.webp");
        updated.ContentType.Should().Be("image/webp");
        updated.Status.Should().Be(FileStatus.Ready);
        updated.ObjectKey.Should().NotBe(oldObjectKey);

        storage.UploadCalls.Should().HaveCount(1);
        storage.DeleteCalls.Should().HaveCount(1);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.OriginalFileName.Should().Be("new.webp");
        saved.ContentType.Should().Be("image/webp");
        saved.Status.Should().Be(FileStatus.Ready);
    }
    
    [Fact]
    public async Task UpdateAsync_NonSeekableStreamWithoutExtension_SavesSizeAsZero_AndObjectKeyWithoutExtension()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var storage = new FakeObjectStorage();
        var service = CreateService(storage);

        await using var stream = new NonSeekableStream("new-non-seekable-content");

        var updated = await service.UpdateAsync(
            metadata.Id,
            stream,
            "README",
            "text/plain",
            UserId);

        updated.Size.Should().Be(0);
        updated.Extension.Should().BeEmpty();
        updated.ObjectKey.Should().NotEndWith(".");
        updated.Status.Should().Be(FileStatus.Ready);

        storage.UploadCalls.Should().HaveCount(1);
    }

    private async Task<FileMetadata> AddMetadataAsync(
        Guid userId,
        FileStatus status,
        FileVisibility visibility)
    {
        var metadata = new FileMetadata
        {
            UserId = userId,
            OriginalFileName = "old.obj",
            Extension = ".obj",
            ContentType = "text/plain",
            Size = 3,
            BucketName = "files",
            ObjectKey = $"{Guid.NewGuid():N}.obj",
            Visibility = visibility,
            Status = status
        };

        Context.FilesMetadata.Add(metadata);

        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        return metadata;
    }

    private static MemoryStream CreateStream(string value)
        => new(System.Text.Encoding.UTF8.GetBytes(value));
    
    private FileService CreateService(IObjectStorage objectStorage)
    {
        return new FileService(
            Context,
            objectStorage,
            NullLogger<FileService>.Instance);
    }
}