namespace VetFlow.Domain.Common;

/// <summary>Thrown when an operation would break a business rule.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Thrown when a requested record does not exist.</summary>
public sealed class NotFoundException(string entityName, object key)
    : DomainException($"{entityName} with id '{key}' was not found.");
