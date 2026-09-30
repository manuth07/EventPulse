using EventPulse.Contracts.Kafka;

namespace EventPulse.BookingService.Consumers;

public interface IDeadLetterPublisher
{
    Task PublishDeadLetterAsync(DeadLetterMessage deadLetter, CancellationToken cancellationToken = default);
}
