using CourseModuleAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CourseModuleAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Course> Courses { get; set; }
        public DbSet<Module> Modules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.CourseId);
                entity.HasMany(e => e.Modules).WithOne(m => m.Course).HasForeignKey(m => m.CourseId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Module>(entity =>
            {
                entity.HasKey(e => e.ModuleId);
            });
        }
    }
}
