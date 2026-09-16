using Hagale.Domain.Common;

namespace Hagale.Domain.Rides;

/// <summary>
/// Mensaje privado intercambiado entre los dos participantes de una carrera.
/// </summary>
public sealed class RideChatMessage
{
    private RideChatMessage()
    {
    }

    public RideChatMessage(
        Guid rideRequestId,
        Guid senderUserId,
        string senderRole,
        string senderName,
        string message,
        DateTimeOffset sentAtUtc)
    {
        if (rideRequestId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El mensaje debe pertenecer a un servicio válido.");
        }

        if (senderUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El mensaje debe identificar a quien lo envía.");
        }

        Id = Guid.NewGuid();
        RideRequestId = rideRequestId;
        SenderUserId = senderUserId;
        SenderRole = RequireText(senderRole, "rol", 30);
        SenderName = RequireText(senderName, "nombre", 200);
        Message = RequireText(message, "mensaje", 500);
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RideRequestId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string SenderRole { get; private set; } = null!;
    public string SenderName { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public DateTimeOffset SentAtUtc { get; private set; }

    private static string RequireText(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new DomainRuleViolationException(
                $"El {label} es obligatorio y no puede superar {maximumLength} caracteres.");
        }

        return normalized;
    }
}
