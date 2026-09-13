namespace Hagale.Infrastructure.Authentication;

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "Authentication:Google";

    public string? ClientId { get; init; }
}
