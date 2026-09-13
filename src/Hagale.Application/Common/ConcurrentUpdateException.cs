namespace Hagale.Application.Common;

public sealed class ConcurrentUpdateException(string message, Exception? innerException = null)
    : Exception(message, innerException);
