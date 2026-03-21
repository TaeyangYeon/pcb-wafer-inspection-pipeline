using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InspectionPipeline.Core.Data;

/// <summary>
/// Design-time factory for InspectionDbContext to support EF Core migrations.
/// </summary>
public class InspectionDbContextFactory : IDesignTimeDbContextFactory<InspectionDbContext>
{
    /// <summary>
    /// Creates a new instance of InspectionDbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A configured InspectionDbContext instance.</returns>
    public InspectionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InspectionDbContext>();
        
        // Use SQLite with a temporary database path for migrations
        optionsBuilder.UseSqlite("Data Source=inspection.db");
        
        return new InspectionDbContext(optionsBuilder.Options);
    }
}