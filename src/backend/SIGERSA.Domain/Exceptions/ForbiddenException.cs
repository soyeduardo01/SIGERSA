namespace SIGERSA.Domain.Exceptions;

public sealed class ForbiddenException(string message) : Exception(message);
