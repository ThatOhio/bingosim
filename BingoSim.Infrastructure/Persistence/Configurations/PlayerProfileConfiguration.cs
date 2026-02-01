using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PlayerProfile entity.
/// </summary>
public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("PlayerProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.SkillTimeMultiplier)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Store Capabilities as JSON column
        builder.OwnsMany(p => p.Capabilities, capBuilder =>
        {
            capBuilder.ToJson("Capabilities");
            capBuilder.Property(c => c.Key).HasMaxLength(100);
            capBuilder.Property(c => c.Name).HasMaxLength(200);
        });

        // Store WeeklySchedule as owned entity with Sessions as JSON
        builder.OwnsOne(p => p.WeeklySchedule, scheduleBuilder =>
        {
            scheduleBuilder.ToJson("WeeklySchedule");
            scheduleBuilder.OwnsMany(s => s.Sessions, sessionBuilder =>
            {
                sessionBuilder.Property(ss => ss.DayOfWeek);
                sessionBuilder.Property(ss => ss.StartLocalTime);
                sessionBuilder.Property(ss => ss.DurationMinutes);
            });
        });

        builder.HasIndex(p => p.CreatedAt)
            .IsDescending();
    }
}
