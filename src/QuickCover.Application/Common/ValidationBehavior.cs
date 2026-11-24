using Mediator;

namespace QuickCover.Application.Common;

/// <summary>
/// Mediator pipeline behaviour: runs before every handler, so validation lives in one place
/// instead of being repeated at the top of each handler.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        CancellationToken cancellationToken,
        MessageHandlerDelegate<TMessage, TResponse> next)
    {
        if (message is IValidatable validatable)
        {
            var errors = validatable.Validate().ToList();
            if (errors.Count > 0)
                throw new ValidationException(errors);
        }

        return next(message, cancellationToken);
    }
}
