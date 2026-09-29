using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CourseModuleAPI.Data;
using CourseModuleAPI.Interfaces;
using CourseModuleAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CourseModuleAPI.Repositories
{
    public class ModuleRepository : IModuleRepository
    {
        private readonly ApplicationDbContext _db;
        public ModuleRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddRangeAsync(IEnumerable<Module> modules)
        {
            await _db.Modules.AddRangeAsync(modules);
        }

        public async Task<IEnumerable<Module>> GetByCourseIdAsync(int courseId)
        {
            return await _db.Modules.Where(m => m.CourseId == courseId).OrderBy(m => m.ModuleOrder).ToListAsync();
        }

        public async Task RemoveRangeAsync(IEnumerable<Module> modules)
        {
            _db.Modules.RemoveRange(modules);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
