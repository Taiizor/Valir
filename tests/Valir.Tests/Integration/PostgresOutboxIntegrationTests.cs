using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Valir.Tests.Integration;

/// <summary>
/// Integration tests for PostgreSQL Outbox pattern using Testcontainers.
/// </summary>
public class PostgresOutboxIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private TestDbContext _dbContext = null!;

    public PostgresOutboxIntegrationTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("valir_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new TestDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task OutboxEntry_ShouldBeSavedToDatabase()
    {
        // Arrange
        OutboxEntry entry = new()
        {
            Id = Guid.CreateVersion7(),
            JobType = "TestJob",
            Payload = """{"data":"test"}""",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        // Act
        _dbContext.OutboxEntries.Add(entry);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        OutboxEntry? saved = await _dbContext.OutboxEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("TestJob", saved.JobType);
        Assert.Null(saved.ProcessedAt);
    }

    [Fact]
    public async Task OutboxEntry_ShouldBeMarkedAsProcessed()
    {
        // Arrange
        OutboxEntry entry = new()
        {
            Id = Guid.CreateVersion7(),
            JobType = "ProcessableJob",
            Payload = """{"action":"process"}""",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        _dbContext.OutboxEntries.Add(entry);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        entry.ProcessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        OutboxEntry? processed = await _dbContext.OutboxEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(processed);
        Assert.NotNull(processed.ProcessedAt);
    }

    [Fact]
    public async Task OutboxEntries_ShouldQueryUnprocessedEntries()
    {
        // Arrange
        OutboxEntry unprocessed1 = new()
        {
            Id = Guid.CreateVersion7(),
            JobType = "Job1",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        OutboxEntry unprocessed2 = new()
        {
            Id = Guid.CreateVersion7(),
            JobType = "Job2",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        OutboxEntry processed = new()
        {
            Id = Guid.CreateVersion7(),
            JobType = "ProcessedJob",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessedAt = DateTime.UtcNow
        };

        _dbContext.OutboxEntries.AddRange(unprocessed1, unprocessed2, processed);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<OutboxEntry> unprocessedEntries = await _dbContext.OutboxEntries
            .Where(e => e.ProcessedAt == null)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, unprocessedEntries.Count);
        Assert.All(unprocessedEntries, e => Assert.Null(e.ProcessedAt));
    }
}

/// <summary>
/// Test DbContext for integration tests.
/// </summary>
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxEntry> OutboxEntries => Set<OutboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxEntry>(entity =>
        {
            entity.ToTable("outbox_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobType).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => e.ProcessedAt);
        });
    }
}

/// <summary>
/// Outbox entry entity for testing.
/// </summary>
public class OutboxEntry
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
