using EventPulse.Contracts.Kafka;

namespace EventPulse.EventService.Consumers;

public interface IDeadLetterPublisher
{
    Task PublishDeadLetterAsync(DeadLetterMessage deadLetter, CancellationToken cancellationToken = default);
}
