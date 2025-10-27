using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Webkho_20241021.Models
{
    public class ApplicationDbContext_user : IdentityDbContext<User>
    {
        public ApplicationDbContext_user(DbContextOptions<ApplicationDbContext_user> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Đặt tên bảng là "User"
            builder.Entity<User>().ToTable("User");

            builder.Entity<User>(entity =>
            {
                // Mapping các thuộc tính nếu cần
                entity.Property(u => u.Name).HasColumnName("Name");
                entity.Property(u => u.manv).HasColumnName("manv");  // Mã nhân viên
                entity.Property(u => u.Chucvu).HasColumnName("Chucvu");

                // Không cần thay đổi PasswordHash và UserName vì chúng đã được quản lý bởi IdentityUser
            });

            builder.Entity<User>().Ignore(u => u.AccessFailedCount);
            builder.Entity<User>().Ignore(u => u.ConcurrencyStamp);
            builder.Entity<User>().Ignore(u => u.EmailConfirmed);
            builder.Entity<User>().Ignore(u => u.SecurityStamp);
            builder.Entity<User>().Ignore(u => u.TwoFactorEnabled);
        }
    }
}
