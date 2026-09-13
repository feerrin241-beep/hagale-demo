using Hagale.Domain.Common;
using Hagale.Domain.Safety;

namespace Hagale.Domain.Tests.Safety;

public sealed class EmergencyServiceChannelTests
{
    [Fact]
    public void Channel_normalizes_city_and_contact_information()
    {
        var channel = new EmergencyServiceChannel(
            " buc ",
            EmergencyChannelType.GeneralEmergency,
            " Atención municipal ",
            " 123 ",
            true,
            DateTimeOffset.UtcNow);

        Assert.Equal("BUC", channel.CityCode);
        Assert.Equal("Atención municipal", channel.DisplayName);
        Assert.Equal("123", channel.ContactNumber);
    }

    [Fact]
    public void Channel_rejects_an_invalid_contact_number()
    {
        Assert.Throws<DomainRuleViolationException>(() => new EmergencyServiceChannel(
            "BUC",
            EmergencyChannelType.GeneralEmergency,
            "Atención municipal",
            "sin número",
            true,
            DateTimeOffset.UtcNow));
    }
}
