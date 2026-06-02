using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Minio.Exceptions;
using Planara.Common.Kafka;
using Planara.Files.Data.Domain;
using Planara.Files.Data.Enum;
using Planara.Files.Interfaces;
using Planara.Files.Tests.Fakes;
using Planara.Files.Workers;

namespace Planara.Files.Tests.Api;

public class KafkaConsumerWorkerTests : BaseApiTest
{
    public KafkaConsumerWorkerTests(ApiTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_ExistingFiles_DeletesObjects_MarksFilesDeleted_AndCommits()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var userId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var userFile1 = CreateFile(userId, "bucket", "user/file-1.obj", FileStatus.Ready);
        var userFile2 = CreateFile(userId, "bucket", "user/file-2.png", FileStatus.Ready);
        var alreadyDeletedFile = CreateFile(userId, "bucket", "user/deleted.webp", FileStatus.Deleted);
        var anotherUserFile = CreateFile(anotherUserId, "bucket", "another/file.obj", FileStatus.Ready);

        Context.FilesMetadata.AddRange(
            userFile1,
            userFile2,
            alreadyDeletedFile,
            anotherUserFile);

        await Context.SaveChangesAsync();

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage();

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        fakeConsumer.Results.Enqueue(FakeKafkaConsumer<UserDeletedMessage>.CreateResult(
            new UserDeletedMessage
            {
                UserId = userId
            }));

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.ConsumedTopicKeys.Should().ContainSingle()
            .Which.Should().Be(KafkaTopicKeys.UserDeleted);

        fakeConsumer.Committed.Should().HaveCount(1);

        fakeStorage.DeleteCalls.Should().HaveCount(2);
        fakeStorage.DeleteCalls.Should().Contain(x => x.Key == "user/file-1.obj");
        fakeStorage.DeleteCalls.Should().Contain(x => x.Key == "user/file-2.png");
        fakeStorage.DeleteCalls.Should().NotContain(x => x.Key == "user/deleted.webp");
        fakeStorage.DeleteCalls.Should().NotContain(x => x.Key == "another/file.obj");

        Context.ChangeTracker.Clear();

        var files = await Context.FilesMetadata
            .AsNoTracking()
            .ToArrayAsync();

        var savedUserFile1 = files.Single(x => x.Id == userFile1.Id);
        savedUserFile1.Status.Should().Be(FileStatus.Deleted);
        savedUserFile1.DeletedAt.Should().NotBeNull();

        var savedUserFile2 = files.Single(x => x.Id == userFile2.Id);
        savedUserFile2.Status.Should().Be(FileStatus.Deleted);
        savedUserFile2.DeletedAt.Should().NotBeNull();

        var savedAlreadyDeletedFile = files.Single(x => x.Id == alreadyDeletedFile.Id);
        savedAlreadyDeletedFile.Status.Should().Be(FileStatus.Deleted);

        var savedAnotherUserFile = files.Single(x => x.Id == anotherUserFile.Id);
        savedAnotherUserFile.Status.Should().Be(FileStatus.Ready);
        savedAnotherUserFile.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_WhenObjectNotFound_MarksFileDeleted_AndCommits()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var userId = Guid.NewGuid();

        var file = CreateFile(userId, "bucket", "missing/file.obj", FileStatus.Ready);

        Context.FilesMetadata.Add(file);

        await Context.SaveChangesAsync();

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage
        {
            ThrowOnDelete = true,
            DeleteException = CreateObjectNotFoundException()
        };

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        fakeConsumer.Results.Enqueue(FakeKafkaConsumer<UserDeletedMessage>.CreateResult(
            new UserDeletedMessage
            {
                UserId = userId
            }));

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.ConsumedTopicKeys.Should().ContainSingle()
            .Which.Should().Be(KafkaTopicKeys.UserDeleted);

        fakeConsumer.Committed.Should().HaveCount(1);

        fakeStorage.DeleteCalls.Should().HaveCount(1);
        fakeStorage.DeleteCalls[0].Bucket.Should().Be("bucket");
        fakeStorage.DeleteCalls[0].Key.Should().Be("missing/file.obj");

        Context.ChangeTracker.Clear();

        var saved = await Context.FilesMetadata
            .AsNoTracking()
            .SingleAsync(x => x.Id == file.Id);

        saved.Status.Should().Be(FileStatus.Deleted);
        saved.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_WhenUserHasNoFiles_Commits()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage();

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        fakeConsumer.Results.Enqueue(FakeKafkaConsumer<UserDeletedMessage>.CreateResult(
            new UserDeletedMessage
            {
                UserId = Guid.NewGuid()
            }));

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.ConsumedTopicKeys.Should().ContainSingle()
            .Which.Should().Be(KafkaTopicKeys.UserDeleted);

        fakeConsumer.Committed.Should().HaveCount(1);
        fakeStorage.DeleteCalls.Should().BeEmpty();

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_NullResult_DoesNotCommit()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage();

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.ConsumedTopicKeys.Should().ContainSingle()
            .Which.Should().Be(KafkaTopicKeys.UserDeleted);

        fakeConsumer.Committed.Should().BeEmpty();
        fakeStorage.DeleteCalls.Should().BeEmpty();

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_NullMessage_DoesNotCommit()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage();

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        fakeConsumer.Results.Enqueue(
            FakeKafkaConsumer<UserDeletedMessage>.CreateNullMessageResult());

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.Committed.Should().BeEmpty();
        fakeStorage.DeleteCalls.Should().BeEmpty();

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task UserDeletedConsumer_ConsumeOnce_NullInnerMessage_DoesNotCommit()
    {
        await DbTestUtils.ResetFilesDbAsync(Context);

        var fakeConsumer = new FakeKafkaConsumer<UserDeletedMessage>();
        var fakeStorage = new FakeObjectStorage();

        var worker = CreateWorker(fakeConsumer, fakeStorage);

        fakeConsumer.Results.Enqueue(
            FakeKafkaConsumer<UserDeletedMessage>.CreateNullInnerMessageResult());

        await worker.ConsumeOnce(CancellationToken.None);

        fakeConsumer.Committed.Should().BeEmpty();
        fakeStorage.DeleteCalls.Should().BeEmpty();

        var count = await Context.FilesMetadata.CountAsync();

        count.Should().Be(0);
    }

    private UserDeletedKafkaConsumerWorker CreateWorker(
        FakeKafkaConsumer<UserDeletedMessage> fakeConsumer,
        FakeObjectStorage objectStorage)
    {
        var services = new ServiceCollection();

        services.AddSingleton(Context);
        services.AddSingleton<IObjectStorage>(objectStorage);

        var provider = services.BuildServiceProvider();

        return new UserDeletedKafkaConsumerWorker(
            NullLogger<UserDeletedKafkaConsumerWorker>.Instance,
            fakeConsumer,
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static FileMetadata CreateFile(
        Guid userId,
        string bucket,
        string key,
        FileStatus status)
    {
        return new FileMetadata
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalFileName = Path.GetFileName(key),
            Extension = Path.GetExtension(key),
            ContentType = "application/octet-stream",
            Size = 100,
            BucketName = bucket,
            ObjectKey = key,
            Visibility = FileVisibility.Private,
            Status = status,
            DeletedAt = status == FileStatus.Deleted
                ? DateTimeOffset.UtcNow
                : null
        };
    }

    private static ObjectNotFoundException CreateObjectNotFoundException()
    {
        return (ObjectNotFoundException)Activator.CreateInstance(
            typeof(ObjectNotFoundException),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["Object not found"],
            culture: null)!;
    }
}