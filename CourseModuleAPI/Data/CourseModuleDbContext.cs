using Microsoft.EntityFrameworkCore;
using CourseModuleAPI.Models;

namespace CourseModuleAPI.Data
{
    public class CourseModuleDbContext : DbContext
    {
        public CourseModuleDbContext(DbContextOptions<CourseModuleDbContext> options)
            : base(options)
        {
        }

        public DbSet<Module> Modules { get; set; }
    }
}