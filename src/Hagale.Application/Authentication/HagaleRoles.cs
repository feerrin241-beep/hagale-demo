namespace Hagale.Application.Authentication;

public static class HagaleRoles
{
    public const string Customer = "Customer";
    public const string Driver = "Driver";
    public const string Administrator = "Administrator";

    public static readonly IReadOnlyCollection<string> All = [Customer, Driver, Administrator];
}
