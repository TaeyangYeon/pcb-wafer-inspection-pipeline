using Microsoft.EntityFrameworkCore;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Data;

/// <summary>
/// Entity Framework Core database context for the inspection system.
/// </summary>
public class InspectionDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the InspectionDbContext class.
    /// </summary>
    /// <param name="options">The DbContext options configured via dependency injection.</param>
    public InspectionDbContext(DbContextOptions<InspectionDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// DbSet for inspection records - the main table storing inspection results.
    /// </summary>
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();

    /// <summary>
    /// DbSet for defect details - child records containing YOLOv8-seg detection results.
    /// </summary>
    public DbSet<DefectDetail> DefectDetails => Set<DefectDetail>();

    /// <summary>
    /// DbSet for model versions - tracking which models were used for inspections.
    /// </summary>
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();

    /// <summary>
    /// Configures the model using Fluent API for optimal performance and data integrity.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureInspectionRecord(modelBuilder);
        ConfigureDefectDetail(modelBuilder);
        ConfigureModelVersion(modelBuilder);
    }

    /// <summary>
    /// Configures the InspectionRecord entity with indexes and constraints.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    private static void ConfigureInspectionRecord(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InspectionRecord>();

        // Primary key is already configured via [Key] annotation
        
        // Index on Timestamp for efficient date range queries
        entity.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_InspectionRecords_Timestamp");

        // Index on FinalResult for efficient filtering by result type
        entity.HasIndex(e => e.FinalResult)
            .HasDatabaseName("IX_InspectionRecords_FinalResult");

        // Composite index for common queries (timestamp + result)
        entity.HasIndex(e => new { e.Timestamp, e.FinalResult })
            .HasDatabaseName("IX_InspectionRecords_Timestamp_FinalResult");

        // Configure string properties with appropriate lengths
        entity.Property(e => e.ImagePath)
            .HasMaxLength(500)
            .IsRequired();

        entity.Property(e => e.FinalResult)
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(e => e.StageUsed)
            .HasMaxLength(20)
            .IsRequired();

        // Configure precision for double properties
        entity.Property(e => e.AnomalyScore)
            .HasPrecision(18, 6);

        entity.Property(e => e.InferenceTimeMs)
            .HasPrecision(18, 3);

        // Configure relationship with ModelVersion
        entity.HasOne(e => e.ModelVersion)
            .WithMany(m => m.InspectionRecords)
            .HasForeignKey(e => e.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of ModelVersion if referenced
    }

    /// <summary>
    /// Configures the DefectDetail entity with cascade delete and indexes.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    private static void ConfigureDefectDetail(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DefectDetail>();

        // Index on InspectionRecordId for efficient child record queries
        entity.HasIndex(e => e.InspectionRecordId)
            .HasDatabaseName("IX_DefectDetails_InspectionRecordId");

        // Index on ClassName for defect type filtering
        entity.HasIndex(e => e.ClassName)
            .HasDatabaseName("IX_DefectDetails_ClassName");

        // Configure string properties
        entity.Property(e => e.ClassName)
            .HasMaxLength(50)
            .IsRequired();

        // Configure precision for double properties
        entity.Property(e => e.Confidence)
            .HasPrecision(18, 6);

        entity.Property(e => e.BboxX)
            .HasPrecision(18, 3);

        entity.Property(e => e.BboxY)
            .HasPrecision(18, 3);

        entity.Property(e => e.BboxW)
            .HasPrecision(18, 3);

        entity.Property(e => e.BboxH)
            .HasPrecision(18, 3);

        // Configure relationship with InspectionRecord (cascade delete)
        entity.HasOne(e => e.InspectionRecord)
            .WithMany(i => i.DefectDetails)
            .HasForeignKey(e => e.InspectionRecordId)
            .OnDelete(DeleteBehavior.Cascade); // Delete defect details when parent inspection is deleted
    }

    /// <summary>
    /// Configures the ModelVersion entity with indexes and constraints.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    private static void ConfigureModelVersion(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ModelVersion>();

        // Index on ModelName for efficient model lookup
        entity.HasIndex(e => e.ModelName)
            .HasDatabaseName("IX_ModelVersions_ModelName");

        // Unique constraint on ModelName + Version combination
        entity.HasIndex(e => new { e.ModelName, e.Version })
            .IsUnique()
            .HasDatabaseName("IX_ModelVersions_ModelName_Version_Unique");

        // Configure string properties
        entity.Property(e => e.ModelName)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(e => e.Version)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(e => e.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        // Configure LoadedAt with default value
        entity.Property(e => e.LoadedAt)
            .HasDefaultValueSql("datetime('now')");
    }
}