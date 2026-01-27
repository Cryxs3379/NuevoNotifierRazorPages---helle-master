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
        // MissedCallsWithClientName eliminado - se usa IncomingNoAtendidas24h en su lugar
        public DbSet<NotifierCallsStaging> NotifierCallsStaging { get; set; }
        public DbSet<Outgoing24hRow> Outgoing24h { get; set; }
        public DbSet<IncomingNoAtendidas24hRow> IncomingNoAtendidas24h { get; set; }
        public DbSet<IncomingAtendidas24hRow> IncomingAtendidas24h { get; set; }

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

            // Vistas keyless para las 3 nuevas pestañas
            modelBuilder.Entity<Outgoing24hRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_Outgoing_24h_ConCliente", "dbo");
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.NombreCompleto).HasMaxLength(255);
                entity.Property(e => e.NombrePila).HasMaxLength(255);
            });

            modelBuilder.Entity<IncomingNoAtendidas24hRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_Incoming_NoAtendidas_24h_ConCliente", "dbo");
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.NombreCompleto).HasMaxLength(255);
                entity.Property(e => e.NombrePila).HasMaxLength(255);
            });

            modelBuilder.Entity<IncomingAtendidas24hRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_Incoming_Atendidas_24h_ConCliente", "dbo");
                entity.Property(e => e.DateAndTime).HasColumnType("datetime2");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.NombreCompleto).HasMaxLength(255);
                entity.Property(e => e.NombrePila).HasMaxLength(255);
            });
        }
    }
}
