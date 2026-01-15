using Microsoft.EntityFrameworkCore;

namespace NotifierAPI.Data;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotifierSmsMessage> SmsMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotifierSmsMessage>(entity =>
        {
            entity.ToTable("NotifierSmsMessages", "dbo");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .HasColumnType("bigint")
                .ValueGeneratedOnAdd();
            
            entity.Property(e => e.Originator)
                .HasColumnName("Originator")
                .HasColumnType("nvarchar(50)")
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.Recipient)
                .HasColumnName("Recipient")
                .HasColumnType("nvarchar(50)")
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.Body)
                .HasColumnName("Body")
                .HasColumnType("nvarchar(max)")
                .IsRequired();
            
            entity.Property(e => e.Type)
                .HasColumnName("Type")
                .HasColumnType("nvarchar(50)")
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.Direction)
                .HasColumnName("Direction")
                .HasColumnType("tinyint")
                .IsRequired();
            
            entity.Property(e => e.MessageAt)
                .HasColumnName("MessageAt")
                .HasColumnType("datetime2(0)")
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("datetime2(0)")
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");
            
            entity.Property(e => e.ProviderMessageId)
                .HasColumnName("ProviderMessageId")
                .HasColumnType("nvarchar(100)")
                .HasMaxLength(100)
                .IsRequired(false);
        });
    }
}
