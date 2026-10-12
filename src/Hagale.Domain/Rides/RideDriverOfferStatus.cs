namespace Hagale.Domain.Rides;

/// <summary>
/// Estado de una propuesta enviada por un conductor para una solicitud.
/// La propuesta no asigna el viaje por sí sola: el pasajero elige una.
/// </summary>
public enum RideDriverOfferStatus
{
    Pending = 1,
    Selected = 2,
    NotSelected = 3,
    Expired = 4,
    Withdrawn = 5
}
