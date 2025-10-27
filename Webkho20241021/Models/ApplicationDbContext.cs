using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;




namespace Webkho_20241021.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> User { get; set; }
        public DbSet<duans> duans { get; set; }
        public DbSet<khoduans> khoduans { get; set; }
        public DbSet<khonguoidungs> khonguoidungs { get; set; }
        public DbSet<khotongs> khotongs { get; set; }
        public DbSet<nguoidungs> nguoidungs { get; set; }
        public DbSet<phieumuahang> phieumuahang { get; set; }
        public DbSet<phieunhapkho> phieunhapkho { get; set; }
        public DbSet<phieuxuatkho> phieuxuatkho { get; set; }
        public DbSet<vtphieumuahang> vtphieumuahang { get; set; }
        public DbSet<vtphieuxuatkho> vtphieuxuatkho { get; set; }
        public DbSet<vtphieunhapkho> vtphieunhapkho { get; set; }
        public DbSet<vtyeucau> vtyeucau { get; set; }
        public DbSet<yeucau> yeucau { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<khoduans>()
                .HasKey(k => new { k.DAMaDuan, k.DAMakho });

            modelBuilder.Entity<khonguoidungs>()
                .HasKey(k => new { k.NDMaNguoidung, k.NDMakho });

            modelBuilder.Entity<phieumuahang>()
                .HasKey(k => new { k.MaMuahang, k.MaYeucau });

            modelBuilder.Entity<phieunhapkho>()
                .HasKey(k => new { k.MaNhapkho, k.MaYeucau });

            modelBuilder.Entity<phieuxuatkho>()
                .HasKey(k => new { k.MaXuatkho, k.MaYeucau });

            base.OnModelCreating(modelBuilder);
        }

    }

}
