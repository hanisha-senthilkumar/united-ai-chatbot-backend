using System.Threading.Tasks;

namespace CourseModuleAPI.Interfaces
{
    public interface ICourseScraperService
    {
        /// <summary>
        /// Scrape the courses site and insert/update courses in the database.
        /// Returns the number of courses added or updated.
        /// </summary>
        Task<int> ScrapeAndSaveAsync();

        /// <summary>
        /// Force refresh (same as ScrapeAndSaveAsync but kept for explicit intent).
        /// </summary>
        Task<int> RefreshAsync();
    }
}
