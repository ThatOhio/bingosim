using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

public class EventSnapshotConfiguration : IEntityTypeConfiguration<EventSnapshot>
{
    public void Configure(EntityTypeBuilder<EventSnapshot> builder)
    {
        builder.ToTable("EventSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SimulationBatchId).IsRequired();
        builder.Property(s => s.EventConfigJson).IsRequired().HasColumnType("jsonb");
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.SimulationBatchId).IsUnique();
    }
}
