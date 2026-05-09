using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace BAP.DAL
{
    public partial class u0987408_AcdmyContext : DbContext
    {
        public u0987408_AcdmyContext()
        {
        }

        public u0987408_AcdmyContext(DbContextOptions<u0987408_AcdmyContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Demand> Demand { get; set; }
        public virtual DbSet<DueDate> DueDate { get; set; }
        public virtual DbSet<GeneralParam> GeneralParam { get; set; }
        public virtual DbSet<Instance> Instance { get; set; }
        public virtual DbSet<ProcessingTime> ProcessingTime { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Server=94.73.146.3;Initial Catalog=u0987408_Acdmy;Persist Security Info=True;User ID=u0987408_user23E;Password=7VPK-0M-0_zw2g=d;MultipleActiveResultSets=True;Encrypt=True; TrustServerCertificate=True");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Demand>(entity =>
            {
                entity.HasNoKey();
            });

            modelBuilder.Entity<DueDate>(entity =>
            {
                entity.HasNoKey();
            });

            modelBuilder.Entity<GeneralParam>(entity =>
            {
                entity.HasNoKey();
            });

            modelBuilder.Entity<Instance>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.IdProcess)
                    .HasColumnName("ID_Process")
                    .HasMaxLength(100);

                entity.Property(e => e.IdProcessInstance)
                    .HasColumnName("ID_Process_Instance")
                    .HasMaxLength(100);

                entity.Property(e => e.Software1).HasColumnName("Software-1");

                entity.Property(e => e.Software2).HasColumnName("Software-2");

                entity.Property(e => e.Software3).HasColumnName("Software-3");
            });

            modelBuilder.Entity<ProcessingTime>(entity =>
            {
                entity.HasNoKey();
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
