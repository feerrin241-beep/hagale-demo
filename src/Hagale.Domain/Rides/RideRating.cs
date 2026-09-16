using Hagale.Domain.Common;

namespace Hagale.Domain.Rides;

/// <summary>
/// Calificación emitida por un participante al finalizar una carrera.
/// </summary>
public sealed class RideRating
{
    private RideRating()
    {
    }

    public RideRating(
        Guid rideRequestId,
        Guid raterUserId,
        string raterRole,
        string ratedRole,
        int score,
        string? comment,
        DateTimeOffset ratedAtUtc)
    {
        if (rideRequestId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La calificación debe pertenecer a un servicio válido.");
        }

        if (raterUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La calificación debe identificar a quien la emite.");
        }

        if (score is < 1 or > 5)
        {
            throw new DomainRuleViolationException("La calificación debe estar entre 1 y 5 estrellas.");
        }

        Id = Guid.NewGuid();
        RideRequestId = rideRequestId;
        RaterUserId = raterUserId;
        RaterRole = RequireRole(raterRole, "quien califica");
        RatedRole = RequireRole(ratedRole, "quien recibe la calificación");
        Score = score;
        Comment = NormalizeComment(comment);
        RatedAtUtc = ratedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RideRequestId { get; private set; }
    public Guid RaterUserId { get; private set; }
    public string RaterRole { get; private set; } = null!;
    public string RatedRole { get; private set; } = null!;
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset RatedAtUtc { get; private set; }

    private static string RequireRole(string? value, string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 30)
        {
            throw new DomainRuleViolationException($"El rol de {label} no es válido.");
        }

        return normalized;
    }

    private static string? NormalizeComment(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 240)
        {
            throw new DomainRuleViolationException("El comentario no puede superar 240 caracteres.");
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
