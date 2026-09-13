namespace Hagale.Domain.Rides;

public enum RideRequestStatus
{
    Pending = 1,
    Cancelled = 2,
    Accepted = 3,
    DriverEnRoute = 4,
    InProgress = 5,
    Completed = 6,
    CounterOfferPending = 7,
    DriverArrived = 8
}
