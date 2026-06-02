using System.Diagnostics.CodeAnalysis;
using Planara.Kafka.Exceptions;
using Planara.Kafka.Interfaces;

namespace Planara.Files.Workers;

public abstract class KafkaConsumerWorkerBase<TMessage>(
    ILogger logger,
    IKafkaConsumer<TMessage> consumer,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
    where TMessage : class
{
    protected abstract string TopicKey { get; }

    [ExcludeFromCodeCoverage]
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{WorkerName} started. Topic: {TopicKey}", GetType().Name, TopicKey);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeOnce(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("{WorkerName} cancellation requested.", GetType().Name);

                break;
            }
            catch (KafkaConsumeException ex)
            {
                logger.LogError(ex, "Failed to consume Kafka message in {WorkerName}.", GetType().Name);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in {WorkerName}.", GetType().Name);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        try
        {
            consumer.Close();

            logger.LogInformation("{WorkerName} Kafka consumer closed.", GetType().Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing Kafka consumer in {WorkerName}.", GetType().Name);
        }
    }

    public async Task ConsumeOnce(CancellationToken cancellationToken)
    {
        var result = await consumer.ConsumeAsync(TopicKey, cancellationToken);

        if (result is null) return;

        if (result.Message is null) return;

        var message = result.Message.Value;

        if (message is null) return;

        await using var scope = scopeFactory.CreateAsyncScope();

        await HandleMessage(message, scope.ServiceProvider, cancellationToken);

        await consumer.CommitAsync(result, cancellationToken);
    }

    protected abstract Task HandleMessage(
        TMessage message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}