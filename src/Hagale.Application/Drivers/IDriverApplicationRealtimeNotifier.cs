namespace Hagale.Application.Drivers;

// Puerto de salida para avisar cambios de habilitación. El evento es
// deliberadamente vacío: la cuenta y administración vuelven a consultar sus
// endpoints autorizados, sin exponer datos de documentos por el transporte.
public interface IDriverApplicationRealtimeNotifier
{
    Task NotifyChangedAsync(
        Guid applicantUserId,
        CancellationToken cancellationToken = default);
}

public sealed class NullDriverApplicationRealtimeNotifier : IDriverApplicationRealtimeNotifier
{
    public static NullDriverApplicationRealtimeNotifier Instance { get; } = new();

    private NullDriverApplicationRealtimeNotifier()
    {
    }

    public Task NotifyChangedAsync(
        Guid applicantUserId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
