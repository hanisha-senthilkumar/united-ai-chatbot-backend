using System.Collections.Generic;
using System.Threading.Tasks;
using CourseModuleAPI.DTOs;

namespace CourseModuleAPI.Interfaces
{
    public interface ICourseService
    {
        Task<IEnumerable<CourseDto>> GetAllAsync();
        Task<CourseDto> GetByIdAsync(int id);
        Task<IEnumerable<CourseDto>> SearchByNameAsync(string name);
        Task<IEnumerable<ModuleDto>> GetModulesByCourseIdAsync(int courseId);
    }
}
