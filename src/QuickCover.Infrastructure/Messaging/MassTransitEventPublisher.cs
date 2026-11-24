using MassTransit;
using QuickCover.Application.Common;

namespace QuickCover.Infrastructure.Messaging;

/// <summary>
/// Because the bus outbox is enabled, Publish here only writes to the OutboxMessage table
/// in the current DbContext. Nothing reaches RabbitMQ until SaveChanges commits.
/// </summary>
internal sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken ct) where T : class =>
        publishEndpoint.Publish(message, ct);
}
