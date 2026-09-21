using Hagale.Domain.Drivers;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;
using Hagale.Domain.Safety;
using Hagale.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hagale.Infrastructure.Persistence;

public sealed class HagaleDbContext(DbContextOptions<HagaleDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<StoredDocumentBlob> StoredDocumentBlobs => Set<StoredDocumentBlob>();
    public DbSet<RideRequest> RideRequests => Set<RideRequest>();
    public DbSet<RideChatMessage> RideChatMessages => Set<RideChatMessage>();
    public DbSet<RideRating> RideRatings => Set<RideRating>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<EmergencyServiceChannel> EmergencyServiceChannels => Set<EmergencyServiceChannel>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampApplicationConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampApplicationConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var usesPostgres = Database.IsNpgsql();

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.RegisteredAtUtc).IsRequired();
            entity.Property(user => user.IsActive).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        });

        builder.Entity<DriverProfile>(entity =>
        {
            entity.ToTable("DriverProfiles");
            entity.HasKey(driver => driver.Id);
            entity.HasIndex(driver => driver.UserId).IsUnique();
            entity.Property(driver => driver.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(driver => driver.AvailabilityStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(driver => driver.AdministrativeNotes).HasMaxLength(1_000);
            entity.Property(driver => driver.LastKnownLatitude).HasPrecision(9, 6);
            entity.Property(driver => driver.LastKnownLongitude).HasPrecision(9, 6);
            ConfigureConcurrencyToken(entity.Property(driver => driver.RowVersion), usesPostgres);
            entity.HasOne<AppUser>()
                .WithOne()
                .HasForeignKey<DriverProfile>(driver => driver.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(driver => driver.Vehicles)
                .WithOne()
                .HasForeignKey(vehicle => vehicle.DriverProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(driver => driver.Documents)
                .WithOne()
                .HasForeignKey(document => document.DriverProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicles");
            entity.HasKey(vehicle => vehicle.Id);
            entity.HasIndex(vehicle => vehicle.Plate).IsUnique();
            entity.Property(vehicle => vehicle.Brand).HasMaxLength(100).IsRequired();
            entity.Property(vehicle => vehicle.Model).HasMaxLength(100).IsRequired();
            entity.Property(vehicle => vehicle.Color).HasMaxLength(50).IsRequired();
            entity.Property(vehicle => vehicle.Plate).HasMaxLength(10).IsRequired();
            entity.Property(vehicle => vehicle.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(vehicle => vehicle.OperatingCityCode).HasMaxLength(20).IsRequired();
        });

        builder.Entity<DriverDocument>(entity =>
        {
            entity.ToTable("DriverDocuments");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(document => document.StorageObjectKey).HasMaxLength(500).IsRequired();
            entity.Property(document => document.ReviewStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(document => document.ReviewNotes).HasMaxLength(1_000);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(document => document.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StoredDocumentBlob>(entity =>
        {
            entity.ToTable("StoredDocumentBlobs");
            entity.HasKey(document => document.StorageObjectKey);
            entity.Property(document => document.StorageObjectKey).HasMaxLength(500).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(document => document.Content).IsRequired();
            entity.Property(document => document.CreatedAtUtc).IsRequired();
            if (usesPostgres)
            {
                entity.Property(document => document.Content).HasColumnType("bytea");
            }
            entity.HasIndex(document => document.OwnerUserId);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.RequestMethod).HasMaxLength(10).IsRequired();
            entity.Property(log => log.RequestPath).HasMaxLength(500).IsRequired();
            entity.Property(log => log.TraceIdentifier).HasMaxLength(100).IsRequired();
            entity.Property(log => log.OccurredAtUtc).IsRequired();
            entity.HasIndex(log => log.OccurredAtUtc);
            entity.HasIndex(log => log.ActorUserId);
        });

        builder.Entity<RideRequest>(entity =>
        {
            entity.ToTable("RideRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.PickupAddress).HasMaxLength(250).IsRequired();
            entity.Property(request => request.DestinationAddress).HasMaxLength(250).IsRequired();
            entity.Property(request => request.OperatingCityCode).HasMaxLength(20).IsRequired();
            entity.Property(request => request.ServiceType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(request => request.PaymentMethod)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(request => request.FareMode)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(request => request.CancellationReason).HasMaxLength(500);
            entity.Property(request => request.CounterOfferPriceCop);
            entity.Property(request => request.PickupLatitude).HasPrecision(9, 6);
            entity.Property(request => request.PickupLongitude).HasPrecision(9, 6);
            entity.Property(request => request.DestinationLatitude).HasPrecision(9, 6);
            entity.Property(request => request.DestinationLongitude).HasPrecision(9, 6);
            ConfigureConcurrencyToken(entity.Property(request => request.RowVersion), usesPostgres);
            entity.HasIndex(request => new { request.CustomerUserId, request.RequestedAtUtc });
            entity.HasIndex(request => request.Status);
            entity.HasIndex(request => new { request.Status, request.OperatingCityCode, request.ServiceType });
            entity.HasOne<DriverProfile>()
                .WithMany()
                .HasForeignKey(request => request.AssignedDriverProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(request => request.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RideChatMessage>(entity =>
        {
            entity.ToTable("RideChatMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SenderRole).HasMaxLength(30).IsRequired();
            entity.Property(message => message.SenderName).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Message).HasMaxLength(500).IsRequired();
            entity.Property(message => message.SentAtUtc).IsRequired();
            entity.HasIndex(message => new { message.RideRequestId, message.SentAtUtc });
            entity.HasIndex(message => message.SenderUserId);
            entity.HasOne<RideRequest>()
                .WithMany()
                .HasForeignKey(message => message.RideRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RideRating>(entity =>
        {
            entity.ToTable("RideRatings");
            entity.HasKey(rating => rating.Id);
            entity.Property(rating => rating.RaterRole).HasMaxLength(30).IsRequired();
            entity.Property(rating => rating.RatedRole).HasMaxLength(30).IsRequired();
            entity.Property(rating => rating.Comment).HasMaxLength(240);
            entity.Property(rating => rating.RatedAtUtc).IsRequired();
            entity.HasIndex(rating => new { rating.RideRequestId, rating.RaterUserId, rating.RaterRole }).IsUnique();
            entity.HasIndex(rating => new { rating.RideRequestId, rating.RatedAtUtc });
            entity.HasOne<RideRequest>()
                .WithMany()
                .HasForeignKey(rating => rating.RideRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PricingRule>(entity =>
        {
            entity.ToTable("PricingRules");
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.CityCode).HasMaxLength(20).IsRequired();
            entity.Property(rule => rule.ServiceType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(rule => rule.MinimumFareCop).IsRequired();
            entity.Property(rule => rule.BaseFareCop).IsRequired();
            entity.Property(rule => rule.FarePerKilometerCop).IsRequired();
            entity.Property(rule => rule.FarePerMinuteCop).IsRequired();
            entity.Property(rule => rule.IncludedWaitingMinutes)
                .HasDefaultValue(PricingRule.DefaultIncludedWaitingMinutes)
                .IsRequired();
            entity.Property(rule => rule.AdditionalWaitingFarePerMinuteCop)
                .HasDefaultValue(PricingRule.DefaultAdditionalWaitingFarePerMinuteCop)
                .IsRequired();
            entity.Property(rule => rule.FairOfferMinimumPercent)
                .HasDefaultValue(PricingRule.DefaultFairOfferMinimumPercent)
                .IsRequired();
            entity.Property(rule => rule.FavorableOfferMinimumPercent)
                .HasDefaultValue(PricingRule.DefaultFavorableOfferMinimumPercent)
                .IsRequired();
            entity.Property(rule => rule.UpdatedAtUtc).IsRequired();
            ConfigureConcurrencyToken(entity.Property(rule => rule.RowVersion), usesPostgres);
            entity.HasIndex(rule => new { rule.CityCode, rule.ServiceType }).IsUnique();
        });

        builder.Entity<EmergencyContact>(entity =>
        {
            entity.ToTable("EmergencyContacts");
            entity.HasKey(contact => contact.Id);
            entity.Property(contact => contact.Name).HasMaxLength(100).IsRequired();
            entity.Property(contact => contact.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(contact => contact.Relationship).HasMaxLength(100);
            entity.Property(contact => contact.CreatedAtUtc).IsRequired();
            entity.HasIndex(contact => new { contact.UserId, contact.ArchivedAtUtc });
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(contact => contact.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmergencyServiceChannel>(entity =>
        {
            entity.ToTable("EmergencyServiceChannels");
            entity.HasKey(channel => channel.Id);
            entity.Property(channel => channel.CityCode).HasMaxLength(20).IsRequired();
            entity.Property(channel => channel.ChannelType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(channel => channel.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(channel => channel.ContactNumber).HasMaxLength(20).IsRequired();
            entity.Property(channel => channel.UpdatedAtUtc).IsRequired();
            ConfigureConcurrencyToken(entity.Property(channel => channel.RowVersion), usesPostgres);
            entity.HasIndex(channel => new { channel.CityCode, channel.ChannelType }).IsUnique();
        });
    }

    private void StampApplicationConcurrencyTokens()
    {
        var usesInMemory = string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal);
        if (!usesInMemory && !Database.IsNpgsql())
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var rowVersion = entry.Properties.FirstOrDefault(property =>
                string.Equals(property.Metadata.Name, "RowVersion", StringComparison.Ordinal));

            if (rowVersion is not null)
            {
                rowVersion.CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }

    private static void ConfigureConcurrencyToken(
        PropertyBuilder<byte[]> property,
        bool usesPostgres)
    {
        if (usesPostgres)
        {
            property
                .HasColumnType("bytea")
                .IsConcurrencyToken()
                .ValueGeneratedNever()
                .IsRequired();
            return;
        }

        property.IsRowVersion().IsRequired();
    }
}
