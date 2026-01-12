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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IncomingCall>(entity =>
            {
                entity.ToTable("IncomingCall", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            });
        }
    }
}
