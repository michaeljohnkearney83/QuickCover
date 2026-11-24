namespace QuickCover.Domain;

/// <summary>A business rule was broken (e.g. buying an expired quote).</summary>
public sealed class DomainException(string message) : Exception(message);
