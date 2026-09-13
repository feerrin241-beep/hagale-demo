namespace Hagale.Application.Common;

public sealed record ApplicationResult<T>(bool IsSuccess, T? Value, string? Error)
{
    public static ApplicationResult<T> Success(T value) => new(true, value, null);

    public static ApplicationResult<T> Failure(string error) => new(false, default, error);
}
