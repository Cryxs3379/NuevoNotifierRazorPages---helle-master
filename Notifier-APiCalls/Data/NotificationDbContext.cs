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
        }
    }
}
