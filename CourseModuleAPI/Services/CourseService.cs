using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CourseModuleAPI.DTOs;
using CourseModuleAPI.Interfaces;
using CourseModuleAPI.Models;

namespace CourseModuleAPI.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _courseRepository;
        private readonly IModuleRepository _moduleRepository;

        public CourseService(ICourseRepository courseRepository, IModuleRepository moduleRepository)
        {
            _courseRepository = courseRepository;
            _moduleRepository = moduleRepository;
        }

        public async Task<IEnumerable<CourseDto>> GetByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Enumerable.Empty<CourseDto>();

            var courses = await _courseRepository.GetByDomainAsync(domain);
            return courses.Select(MapCourseToDto);
        }

        public Task<int> CountByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Task.FromResult(0);

            return _courseRepository.CountByDomainAsync(domain);
        }

        public async Task<IEnumerable<CourseDto>> GetAllAsync()
        {
            var courses = await _courseRepository.GetAllAsync();
            return courses.Select(MapCourseToDto);
        }

        public async Task<CourseDto> GetByIdAsync(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return null;
            return MapCourseToDto(course);
        }

        public async Task<IEnumerable<CourseDto>> SearchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Enumerable.Empty<CourseDto>();

            // Simple contains search
            var all = await _courseRepository.GetAllAsync();
            var filtered = all.Where(c => c.CourseName != null && c.CourseName.ToLower().Contains(name.ToLower()));
            return filtered.Select(MapCourseToDto);
        }

        public async Task<IEnumerable<ModuleDto>> GetModulesByCourseIdAsync(int courseId)
        {
            var modules = await _moduleRepository.GetByCourseIdAsync(courseId);
            return modules.Select(m => new ModuleDto
            {
                ModuleId = m.ModuleId,
                ModuleName = m.ModuleName,
                ModuleDescription = m.ModuleDescription,
                ModuleOrder = m.ModuleOrder
            });
        }

        private CourseDto MapCourseToDto(Course c)
        {
            return new CourseDto
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                Description = c.Description,
                Category = c.Category,
                Domain = c.Domain,
                Duration = c.Duration,
                SourceUrl = c.SourceUrl,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                Modules = c.Modules?.Select(m => new ModuleDto
                {
                    ModuleId = m.ModuleId,
                    ModuleName = m.ModuleName,
                    ModuleDescription = m.ModuleDescription,
                    ModuleOrder = m.ModuleOrder
                }).OrderBy(m => m.ModuleOrder).ToList() ?? new List<ModuleDto>()
            };
        }
    }
}
