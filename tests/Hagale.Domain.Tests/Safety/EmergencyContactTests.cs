using Hagale.Domain.Common;
using Hagale.Domain.Safety;

namespace Hagale.Domain.Tests.Safety;

public sealed class EmergencyContactTests
{
    [Fact]
    public void Contact_normalizes_its_safe_fields()
    {
        var contact = new EmergencyContact(
            Guid.NewGuid(),
            "  Ana Pérez  ",
            "  +573001234567  ",
            "  Hermana ",
            DateTimeOffset.UtcNow);

        Assert.Equal("Ana Pérez", contact.Name);
        Assert.Equal("+573001234567", contact.PhoneNumber);
        Assert.Equal("Hermana", contact.Relationship);
        Assert.True(contact.IsActive);
    }

    [Fact]
    public void Archived_contact_cannot_be_changed()
    {
        var contact = new EmergencyContact(
            Guid.NewGuid(),
            "Ana Pérez",
            "+573001234567",
            "Hermana",
            DateTimeOffset.UtcNow);
        contact.Archive(DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() =>
            contact.Update("María Gómez", "+573004567890", "Amiga"));
    }
}
