using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CourseModuleAPI.Data;
using CourseModuleAPI.Interfaces;
using CourseModuleAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CourseModuleAPI.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        private readonly ApplicationDbContext _db;
        public CourseRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Course course)
        {
            await _db.Courses.AddAsync(course);
        }

        public async Task<IEnumerable<Course>> GetAllAsync()
        {
            return await _db.Courses.Include(c => c.Modules).ToListAsync();
        }

        public async Task<Course> GetByIdAsync(int id)
        {
            return await _db.Courses.Include(c => c.Modules).FirstOrDefaultAsync(c => c.CourseId == id);
        }

        public async Task<Course> GetByNameAsync(string name)
        {
            return await _db.Courses.Include(c => c.Modules).FirstOrDefaultAsync(c => c.CourseName.ToLower() == name.ToLower());
        }

        public async Task<bool> ExistsAsync(string sourceUrl, string courseName)
        {
            return await _db.Courses.AnyAsync(c => c.SourceUrl == sourceUrl && c.CourseName == courseName);
        }

        public async Task UpdateAsync(Course course)
        {
            _db.Courses.Update(course);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
