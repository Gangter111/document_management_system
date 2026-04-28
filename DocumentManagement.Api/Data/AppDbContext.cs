using DocumentFormat.OpenXml.InkML;
using DocumentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace DocumentManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(x => x.DocumentNumber);
            entity.HasIndex(x => x.Title);
            entity.HasIndex(x => x.Signer);
            entity.HasIndex(x => x.IssuingOrganization);
            entity.HasIndex(x => x.IssuedDate);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsDeleted);

            entity.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.IssuingOrganization).HasMaxLength(300);
            entity.Property(x => x.Signer).HasMaxLength(200);
            entity.Property(x => x.ResponsibleDepartment).HasMaxLength(200);
            entity.Property(x => x.ConfidentialLevel).HasMaxLength(50);
            entity.Property(x => x.UrgencyLevel).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.EntityName);
            entity.HasIndex(x => x.EntityId);
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}