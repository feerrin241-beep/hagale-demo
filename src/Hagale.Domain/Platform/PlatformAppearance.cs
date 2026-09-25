namespace Hagale.Domain.Platform;

/// <summary>Configuración visual y de mensajes editables desde el Centro de diseño.</summary>
public sealed class PlatformAppearance
{
    public const int SingletonId = 1;

    private PlatformAppearance() { }

    public PlatformAppearance(DateTimeOffset updatedAtUtc)
    {
        Id = SingletonId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public int Id { get; private set; }
    public string AccentColor { get; private set; } = "#FFD800";
    public string ActionColor { get; private set; } = "#9BE8B8";
    public string BusyColor { get; private set; } = "#D84545";
    public string CustomerModeLabel { get; private set; } = "CLIENTE";
    public string DriverModeLabel { get; private set; } = "CONDUCTOR";
    public string FreeStatusLabel { get; private set; } = "LIBRE";
    public string BusyStatusLabel { get; private set; } = "OCUPADO";
    public string RequestActionLabel { get; private set; } = "PEDIR MOTO";
    public string DriverOfferVoiceTemplate { get; private set; } = "Nuevo servicio Hágale. Recoger en {origen}. Entregar en {destino}. Valor ofrecido {valor} pesos.";
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        string accentColor,
        string actionColor,
        string busyColor,
        string customerModeLabel,
        string driverModeLabel,
        string freeStatusLabel,
        string busyStatusLabel,
        string requestActionLabel,
        string driverOfferVoiceTemplate,
        DateTimeOffset updatedAtUtc)
    {
        AccentColor = accentColor;
        ActionColor = actionColor;
        BusyColor = busyColor;
        CustomerModeLabel = customerModeLabel;
        DriverModeLabel = driverModeLabel;
        FreeStatusLabel = freeStatusLabel;
        BusyStatusLabel = busyStatusLabel;
        RequestActionLabel = requestActionLabel;
        DriverOfferVoiceTemplate = driverOfferVoiceTemplate;
        UpdatedAtUtc = updatedAtUtc;
    }
}
