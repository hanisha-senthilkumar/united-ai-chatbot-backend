using System;
using System.Linq;
using System.Threading.Tasks;
using CourseModuleAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CourseModuleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ICourseScraperService _scraper;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(ICourseService courseService, ICourseScraperService scraper, ILogger<CoursesController> logger)
        {
            _courseService = courseService;
            _scraper = scraper;
            _logger = logger;
        }

        [HttpGet("domain/{domain}")]
        public async Task<IActionResult> GetByDomain(string domain)
        {
            var results = await _courseService.GetByDomainAsync(domain);
            return Ok(new { success = true, domain, count = results.Count(), results });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var courses = await _courseService.GetAllAsync();
            return Ok(new { success = true, data = courses });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var course = await _courseService.GetByIdAsync(id);
            if (course == null) return NotFound(new { success = false, message = "Course not found." });
            return Ok(new { success = true, data = course });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string name)
        {
            var results = await _courseService.SearchByNameAsync(name);
            return Ok(new { success = true, query = name, results });
        }

        [HttpGet("{id:int}/modules")]
        public async Task<IActionResult> GetModules(int id)
        {
            var modules = await _courseService.GetModulesByCourseIdAsync(id);
            return Ok(new { success = true, courseId = id, modules });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                var count = await _scraper.RefreshAsync();
                return Ok(new { success = true, message = "Refresh completed.", count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing courses");
                return StatusCode(500, new { success = false, message = "Error refreshing courses." });
            }
        }
    }
}
