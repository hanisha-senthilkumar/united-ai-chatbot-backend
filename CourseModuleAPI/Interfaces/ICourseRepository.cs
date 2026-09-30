using System.Collections.Generic;
using System.Threading.Tasks;
using CourseModuleAPI.Models;

namespace CourseModuleAPI.Interfaces
{
    public interface ICourseRepository
    {
        Task<IEnumerable<Course>> GetAllAsync();
        Task<IEnumerable<Course>> GetByDomainAsync(string domain);
        Task<int> CountByDomainAsync(string domain);
        Task<Course> GetByIdAsync(int id);
        Task<Course> GetByNameAsync(string name);
        Task AddAsync(Course course);
        Task UpdateAsync(Course course);
        Task<bool> ExistsAsync(string sourceUrl, string courseName);
        Task SaveChangesAsync();
    }
}
