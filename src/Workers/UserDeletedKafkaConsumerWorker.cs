using Microsoft.EntityFrameworkCore;
using Minio.Exceptions;
using Planara.Common.Kafka;
using Planara.Files.Data;
using Planara.Files.Data.Enum;
using Planara.Files.Interfaces;
using Planara.Kafka.Interfaces;

namespace Planara.Files.Workers;

public class UserDeletedKafkaConsumerWorker(
    ILogger<UserDeletedKafkaConsumerWorker> logger,
    IKafkaConsumer<UserDeletedMessage> consumer,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerWorkerBase<UserDeletedMessage>(logger, consumer, scopeFactory)
{
    protected override string TopicKey => KafkaTopicKeys.UserDeleted;

    protected override async Task HandleMessage(
        UserDeletedMessage message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka: user deleted message received: userId - {UserId}", message.UserId);

        var dbContext = serviceProvider.GetRequiredService<DataContext>();
        var objectStorage = serviceProvider.GetRequiredService<IObjectStorage>();

        var files = await dbContext.FilesMetadata
            .Where(x => x.UserId == message.UserId && x.Status != FileStatus.Deleted)
            .ToListAsync(cancellationToken);

        var deletedCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var file in files)
        {
            try
            {
                await objectStorage.DeleteAsync(
                    file.BucketName,
                    file.ObjectKey,
                    cancellationToken);

                file.Status = FileStatus.Deleted;
                file.DeletedAt ??= now;
                deletedCount++;
            }
            catch (ObjectNotFoundException)
            {
                file.Status = FileStatus.Deleted;
                file.DeletedAt ??= now;
                deletedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Files deletion completed for userId={UserId}. Deleted files: {DeletedCount}",
            message.UserId,
            deletedCount);
    }
}