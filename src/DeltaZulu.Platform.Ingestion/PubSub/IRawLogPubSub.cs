namespace DeltaZulu.Platform.Ingestion.PubSub;

public interface IRawLogPublisher
{
    ValueTask PublishAsync(RawLogBatch batch, CancellationToken cancellationToken = default);
}

public interface IRawLogSubscriber
{
    ValueTask HandleAsync(RawLogBatch batch, CancellationToken cancellationToken = default);
}
