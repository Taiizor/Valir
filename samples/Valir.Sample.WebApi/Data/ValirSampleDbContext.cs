using Microsoft.EntityFrameworkCore;
using Valir.EntityFrameworkCore;

namespace Valir.Sample.WebApi.Data;

public class ValirSampleDbContext(DbContextOptions<ValirSampleDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureValirOutbox();
    }
}
