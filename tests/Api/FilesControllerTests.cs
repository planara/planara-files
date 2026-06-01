using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Planara.Files.Data.Domain;
using Planara.Files.Data.Enum;

namespace Planara.Files.Tests.Api;

public class FilesControllerTests : BaseApiTest
{
    public FilesControllerTests(ApiTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsOk_AndCreatesMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFile(
            "file",
            "image.png",
            "image/png",
            "content");

        var response = await Client.PostAsync("/api/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await response.ReadJsonAsync();

        json.RootElement.GetProperty("originalFileName").GetString().Should().Be("image.png");
        json.RootElement.GetProperty("contentType").GetString().Should().Be("image/png");

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        metadata.UserId.Should().Be(UserId);
        metadata.OriginalFileName.Should().Be("image.png");
        metadata.Status.Should().Be(FileStatus.Ready);
    }

    [Fact]
    public async Task Upload_UnsupportedFile_ReturnsBadRequest_AndDoesNotCreateMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFile(
            "file",
            "virus.exe",
            "application/octet-stream",
            "content");

        var response = await Client.PostAsync("/api/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest_AndDoesNotCreateMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFile(
            "file",
            "image.png",
            "image/png",
            "");

        var response = await Client.PostAsync("/api/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Download_MetadataNotFound_ReturnsNotFound()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var response = await Client.GetAsync($"/api/files/{Guid.NewGuid()}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_ExistingReadyFile_ReturnsFile()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var uploadContent = ApiTestClient.MultipartFile(
            "file",
            "model.obj",
            "text/plain",
            "o Cube");

        var uploadResponse = await Client.PostAsync("/api/files/upload", uploadContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        var response = await Client.GetAsync($"/api/files/{metadata.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Be("o Cube");
    }

    [Fact]
    public async Task Delete_ExistingFile_ReturnsNoContent_AndMarksDeleted()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var metadata = await AddMetadataAsync(
            userId: UserId,
            status: FileStatus.Ready,
            visibility: FileVisibility.Private);

        var response = await Client.DeleteAsync($"/api/files/{metadata.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.Status.Should().Be(FileStatus.Deleted);
    }

    [Fact]
    public async Task Delete_EmptyGuid_ReturnsBadRequest()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var response = await Client.DeleteAsync($"/api/files/{Guid.Empty}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ValidFile_ReturnsOk_AndUpdatesMetadata()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var oldContent = ApiTestClient.MultipartFile(
            "file",
            "old.obj",
            "text/plain",
            "old");

        var uploadResponse = await Client.PostAsync("/api/files/upload", oldContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        using var newContent = ApiTestClient.MultipartFile(
            "file",
            "new.webp",
            "image/webp",
            "new-file");

        var response = await Client.PutAsync($"/api/files/{metadata.Id}", newContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await response.ReadJsonAsync();

        json.RootElement.GetProperty("originalFileName").GetString().Should().Be("new.webp");
        json.RootElement.GetProperty("contentType").GetString().Should().Be("image/webp");

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.OriginalFileName.Should().Be("new.webp");
        saved.ContentType.Should().Be("image/webp");
        saved.Status.Should().Be(FileStatus.Ready);
    }

    [Fact]
    public async Task Update_UnsupportedFile_ReturnsBadRequest()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFile(
            "file",
            "archive.zip",
            "application/zip",
            "content");

        var response = await Client.PutAsync($"/api/files/{Guid.NewGuid()}", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_EmptyGuid_ReturnsBadRequest()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFile(
            "file",
            "image.png",
            "image/png",
            "content");

        var response = await Client.PutAsync($"/api/files/{Guid.Empty}", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Update_EmptyContentType_ReturnsOk_AndUsesOctetStream()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var oldContent = ApiTestClient.MultipartFile(
            "file",
            "old.obj",
            "text/plain",
            "old");

        var uploadResponse = await Client.PostAsync("/api/files/upload", oldContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        using var newContent = ApiTestClient.MultipartFileWithoutContentType(
            "file",
            "new.obj",
            "new-file");

        var response = await Client.PutAsync($"/api/files/{metadata.Id}", newContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == metadata.Id);

        saved.ContentType.Should().Be("application/octet-stream");
    }
    
    [Fact]
    public async Task Upload_EmptyContentType_ReturnsOk_AndUsesOctetStream()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        using var content = ApiTestClient.MultipartFileWithoutContentType(
            "file",
            "model.obj",
            "o Cube");

        var response = await Client.PostAsync("/api/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await response.ReadJsonAsync();

        json.RootElement.GetProperty("contentType").GetString()
            .Should().Be("application/octet-stream");

        Context.ChangeTracker.Clear();

        var metadata = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync();

        metadata.ContentType.Should().Be("application/octet-stream");
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
}