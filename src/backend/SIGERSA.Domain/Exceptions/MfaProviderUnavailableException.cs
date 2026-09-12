namespace SIGERSA.Domain.Exceptions;

public sealed class MfaProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
