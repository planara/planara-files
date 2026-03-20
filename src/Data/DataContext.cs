using Microsoft.EntityFrameworkCore;
using Planara.Files.Data.Domain;

namespace Planara.Files.Data;

public class DataContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<FileMetadata> FilesMetadata { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FileMetadata>()
            .HasKey(x => x.Id);
        
        modelBuilder.Entity<FileMetadata>()
            .Property(x => x.OriginalFileName)
            .HasMaxLength(512)
            .IsRequired();

        modelBuilder.Entity<FileMetadata>()
            .Property(x => x.Extension)
            .HasMaxLength(32);

        modelBuilder.Entity<FileMetadata>()
            .Property(x => x.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        modelBuilder.Entity<FileMetadata>()
            .Property(x => x.BucketName)
            .HasMaxLength(128)
            .IsRequired();

        modelBuilder.Entity<FileMetadata>()
            .Property(x => x.ObjectKey)
            .HasMaxLength(1024)
            .IsRequired();

        modelBuilder.Entity<FileMetadata>()
            .HasIndex(x => x.ObjectKey);
    }
}