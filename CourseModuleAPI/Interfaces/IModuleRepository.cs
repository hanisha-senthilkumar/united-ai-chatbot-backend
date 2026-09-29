using System.Collections.Generic;
using System.Threading.Tasks;
using CourseModuleAPI.Models;

namespace CourseModuleAPI.Interfaces
{
    public interface IModuleRepository
    {
        Task<IEnumerable<Module>> GetByCourseIdAsync(int courseId);
        Task AddRangeAsync(IEnumerable<Module> modules);
        Task RemoveRangeAsync(IEnumerable<Module> modules);
        Task SaveChangesAsync();
    }
}
