using Microsoft.EntityFrameworkCore;
using Valir.EntityFrameworkCore;

namespace Valir.Sample.WebApi.Data;

public class ValirSampleDbContext : DbContext
{
    public ValirSampleDbContext(DbContextOptions<ValirSampleDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureValirOutbox();
    }
}
