namespace SIGERSA.Domain.Exceptions;

public sealed class FileStorageUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
