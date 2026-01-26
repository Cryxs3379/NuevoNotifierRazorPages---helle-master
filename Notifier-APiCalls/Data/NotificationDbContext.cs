using Microsoft.EntityFrameworkCore;
using NotifierAPI.Models;

namespace NotifierAPI.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }

        public DbSet<IncomingCall> IncomingCalls { get; set; }
        public DbSet<MissedCallWithClientNameRow> MissedCallsWithClientName { get; set; }
        public DbSet<NotifierCallsStaging> NotifierCallsStaging { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IncomingCall>(entity =>
            {
                entity.ToTable("NotifierIncomingCalls", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            });

            // Vista keyless para vw_MissedCalls_WithClientName
            modelBuilder.Entity<MissedCallWithClientNameRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_MissedCalls_WithClientName", "dbo");
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            });

            // Tabla NotifierCalls_Staging
            modelBuilder.Entity<NotifierCallsStaging>(entity =>
            {
                entity.ToTable("NotifierCalls_Staging", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.StatusText).HasMaxLength(255);
                entity.Property(e => e.SourceFile).HasMaxLength(500);
                entity.Property(e => e.LoadedAt).HasColumnType("datetime2");
            });
        }
    }
}
