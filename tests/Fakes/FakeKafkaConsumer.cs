using Confluent.Kafka;
using Planara.Kafka.Interfaces;

namespace Planara.Files.Tests.Fakes;

public class FakeKafkaConsumer<TMessage> : IKafkaConsumer<TMessage>
    where TMessage : class
{
    public Queue<ConsumeResult<string, TMessage>?> Results { get; } = [];

    public List<ConsumeResult<string, TMessage>> Committed { get; } = [];

    public List<string> ConsumedTopicKeys { get; } = [];

    public bool Closed { get; private set; }

    public Task<ConsumeResult<string, TMessage>?> ConsumeAsync(
        string topicKey,
        CancellationToken cancellationToken = default)
    {
        ConsumedTopicKeys.Add(topicKey);

        var result = Results.Count > 0
            ? Results.Dequeue()
            : null;

        return Task.FromResult(result);
    }

    public Task CommitAsync(
        ConsumeResult<string, TMessage> result,
        CancellationToken cancellationToken = default)
    {
        Committed.Add(result);

        return Task.CompletedTask;
    }

    public void Close()
    {
        Closed = true;
    }

    public void Reset()
    {
        Results.Clear();
        Committed.Clear();
        ConsumedTopicKeys.Clear();
        Closed = false;
    }

    public static ConsumeResult<string, TMessage> CreateResult(TMessage message)
    {
        return new ConsumeResult<string, TMessage>
        {
            Message = new Message<string, TMessage>
            {
                Key = Guid.NewGuid().ToString("N"),
                Value = message
            },
            Partition = new Partition(0),
            Offset = new Offset(0)
        };
    }

    public static ConsumeResult<string, TMessage> CreateNullMessageResult()
    {
        return new ConsumeResult<string, TMessage>
        {
            Message = null,
            Partition = new Partition(0),
            Offset = new Offset(0)
        };
    }

    public static ConsumeResult<string, TMessage> CreateNullInnerMessageResult()
    {
        return new ConsumeResult<string, TMessage>
        {
            Message = new Message<string, TMessage>
            {
                Key = Guid.NewGuid().ToString("N"),
                Value = null!
            },
            Partition = new Partition(0),
            Offset = new Offset(0)
        };
    }
}