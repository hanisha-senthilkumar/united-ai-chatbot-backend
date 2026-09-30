using System.Collections.Generic;
using System.Threading.Tasks;
using CourseModuleAPI.DTOs;

namespace CourseModuleAPI.Interfaces
{
    public interface ICourseService
    {
        Task<IEnumerable<CourseDto>> GetAllAsync();
        Task<IEnumerable<CourseDto>> GetByDomainAsync(string domain);
        Task<int> CountByDomainAsync(string domain);
        Task<CourseDto> GetByIdAsync(int id);
        Task<IEnumerable<CourseDto>> SearchByNameAsync(string name);
        Task<IEnumerable<ModuleDto>> GetModulesByCourseIdAsync(int courseId);
    }
}
