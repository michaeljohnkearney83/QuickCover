namespace QuickCover.Application.Common;

/// <summary>Input failed validation. Mapped to HTTP 400.</summary>
public sealed class ValidationException(IReadOnlyList<string> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>Resource doesn't exist (or belongs to someone else). Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Request clashes with current state (e.g. already purchased). Mapped to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
