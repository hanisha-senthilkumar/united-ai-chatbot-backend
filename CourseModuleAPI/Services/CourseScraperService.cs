using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CourseModuleAPI.Interfaces;
using CourseModuleAPI.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CourseModuleAPI.Services
{
    public class CourseScraperService : ICourseScraperService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICourseRepository _courseRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly ILogger<CourseScraperService> _logger;

        private const string CoursesUrl = "https://unitedsofttech.co.in/Courses";

        public CourseScraperService(IHttpClientFactory httpClientFactory,
            ICourseRepository courseRepository,
            IModuleRepository moduleRepository,
            ILogger<CourseScraperService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _courseRepository = courseRepository;
            _moduleRepository = moduleRepository;
            _logger = logger;
        }

        public async Task<int> RefreshAsync()
        {
            return await ScrapeAndSaveAsync();
        }

        public async Task<int> ScrapeAndSaveAsync()
        {
            _logger.LogInformation("Starting course scrape from {Url}", CoursesUrl);
            var client = _httpClientFactory.CreateClient("scraper");
            client.Timeout = TimeSpan.FromSeconds(30);

            string html;
            try
            {
                html = await client.GetStringAsync(CoursesUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download courses page");
                throw;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to find course item nodes. Be defensive with multiple selectors.
            var courseNodes = new List<HtmlNode>();

            // Common patterns: cards, course-list, list of anchors
            courseNodes.AddRange(doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' course-card ')]") ?? Enumerable.Empty<HtmlNode>());
            courseNodes.AddRange(doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' course-item ')]") ?? Enumerable.Empty<HtmlNode>());
            courseNodes.AddRange(doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' card ') and .//h3]") ?? Enumerable.Empty<HtmlNode>());
            courseNodes.AddRange(doc.DocumentNode.SelectNodes("//ul[contains(@class,'courses')]/li") ?? Enumerable.Empty<HtmlNode>());

            // The current website renders its course cards from Scripts/coursesData.js,
            // so do not treat navigation links as course records.
            if (!courseNodes.Any())
            {
                var scriptCount = await ScrapeJavaScriptCourseDataAsync(client);
                _logger.LogInformation("Scraping completed. {Count} courses processed.", scriptCount);
                return scriptCount;
            }

            var scrapedCount = 0;

            foreach (var node in courseNodes.Distinct())
            {
                try
                {
                    var (course, modules) = await ParseCourseNodeAsync(node, client);
                    if (course == null || string.IsNullOrWhiteSpace(course.CourseName))
                        continue;

                    // Upsert logic: check by SourceUrl + CourseName if available otherwise by name
                    var exists = false;
                    if (!string.IsNullOrWhiteSpace(course.SourceUrl))
                    {
                        exists = await _courseRepository.ExistsAsync(course.SourceUrl, course.CourseName);
                    }
                    if (!exists)
                    {
                        var existing = await _courseRepository.GetByNameAsync(course.CourseName);
                        exists = existing != null;
                    }

                    if (exists)
                    {
                        // update existing
                        var existing = await _courseRepository.GetByNameAsync(course.CourseName);
                        if (existing != null)
                        {
                            existing.Description = course.Description ?? existing.Description;
                    existing.Category = course.Category ?? existing.Category;
                    existing.Domain = course.Domain ?? existing.Domain;
                            existing.Duration = course.Duration ?? existing.Duration;
                            existing.SourceUrl = course.SourceUrl ?? existing.SourceUrl;
                            existing.UpdatedAt = DateTime.UtcNow;

                            // Replace modules
                            var existingModules = existing.Modules.ToList();
                            if (existingModules.Any())
                            {
                                await _moduleRepository.RemoveRangeAsync(existingModules);
                            }

                            if (modules.Any())
                            {
                                foreach (var m in modules)
                                {
                                    m.CourseId = existing.CourseId;
                                }
                                await _moduleRepository.AddRangeAsync(modules);
                            }

                            await _courseRepository.UpdateAsync(existing);
                            await _courseRepository.SaveChangesAsync();
                            scrapedCount++;
                            _logger.LogInformation("Updated course: {Course}", existing.CourseName);
                        }
                    }
                    else
                    {
                        // create new
                        await _courseRepository.AddAsync(course);
                        await _courseRepository.SaveChangesAsync();

                        if (modules.Any())
                        {
                            foreach (var m in modules)
                            {
                                m.CourseId = course.CourseId;
                            }
                            await _moduleRepository.AddRangeAsync(modules);
                            await _moduleRepository.SaveChangesAsync();
                        }

                        scrapedCount++;
                        _logger.LogInformation("Inserted course: {Course}", course.CourseName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing a course node");
                }
            }

            _logger.LogInformation("Scraping completed. {Count} courses processed.", scrapedCount);
            return scrapedCount;
        }

        private async Task<int> ScrapeJavaScriptCourseDataAsync(HttpClient client)
        {
            var scriptUrl = new Uri(new Uri(CoursesUrl), "/Scripts/coursesData.js").ToString();
            string script;

            try
            {
                script = await client.GetStringAsync(scriptUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download course data script from {Url}", scriptUrl);
                throw;
            }

            var courseObjects = Regex.Matches(
                script,
                @"(?ms)^[ ]{4}\{\s*id\s*:.*?^[ ]{4}\},\s*$")
                .Cast<Match>()
                .ToList();

            var processed = 0;
            foreach (var courseObject in courseObjects)
            {
                var value = courseObject.Value;
                var name = ExtractJavaScriptString(value, "title");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var modules = ExtractJavaScriptArray(value, "syllabus")
                    .Select((moduleName, index) => new Module
                    {
                        ModuleName = moduleName,
                        ModuleDescription = moduleName,
                        ModuleOrder = index + 1
                    })
                    .ToList();

                var course = new Course
                {
                    CourseName = name,
                    Description = FirstNonEmpty(
                        ExtractJavaScriptString(value, "description"),
                        ExtractJavaScriptString(value, "overview")),
                    Category = ExtractJavaScriptString(value, "category"),
                    Domain = FirstNonEmpty(
                        ExtractJavaScriptString(value, "domain"),
                        ExtractJavaScriptString(value, "category")),
                    Duration = ExtractJavaScriptString(value, "duration"),
                    SourceUrl = CoursesUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var existing = await _courseRepository.GetByNameAsync(course.CourseName);
                if (existing == null)
                {
                    await _courseRepository.AddAsync(course);
                    await _courseRepository.SaveChangesAsync();
                    _logger.LogInformation("Inserted course: {Course}", course.CourseName);
                }
                else
                {
                    existing.Description = FirstNonEmpty(course.Description, existing.Description);
                    existing.Category = FirstNonEmpty(course.Category, existing.Category);
                    existing.Domain = FirstNonEmpty(course.Domain, existing.Domain);
                    existing.Duration = FirstNonEmpty(course.Duration, existing.Duration);
                    existing.SourceUrl = course.SourceUrl;
                    existing.UpdatedAt = DateTime.UtcNow;

                    var oldModules = existing.Modules.ToList();
                    if (oldModules.Any())
                        await _moduleRepository.RemoveRangeAsync(oldModules);

                    await _courseRepository.UpdateAsync(existing);
                    await _courseRepository.SaveChangesAsync();
                    course.CourseId = existing.CourseId;
                    _logger.LogInformation("Updated course: {Course}", course.CourseName);
                }

                var persistedCourse = existing ?? course;
                foreach (var module in modules)
                    module.CourseId = persistedCourse.CourseId;

                if (modules.Any())
                {
                    await _moduleRepository.AddRangeAsync(modules);
                    await _moduleRepository.SaveChangesAsync();
                }

                processed++;
            }

            return processed;
        }

        private static string ExtractJavaScriptString(string source, string propertyName)
        {
            var match = Regex.Match(
                source,
                $@"(?ms)^\s*{Regex.Escape(propertyName)}\s*:\s*""(?<value>(?:\\.|[^""])*)""\s*,?");

            return match.Success ? Regex.Unescape(match.Groups["value"].Value).Trim() : null;
        }

        private static IEnumerable<string> ExtractJavaScriptArray(string source, string propertyName)
        {
            var arrayMatch = Regex.Match(
                source,
                $@"(?ms)^\s*{Regex.Escape(propertyName)}\s*:\s*\[(?<items>.*?)\]");

            if (!arrayMatch.Success)
                return Enumerable.Empty<string>();

            return Regex.Matches(arrayMatch.Groups["items"].Value, @"""(?<value>(?:\\.|[^""])*)""")
                .Cast<Match>()
                .Select(match => Regex.Unescape(match.Groups["value"].Value).Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private async Task<(Course, List<Module>)> ParseCourseNodeAsync(HtmlNode node, HttpClient client)
        {
            // Attempt to extract course name
            string name = null;
            string description = null;
            string sourceUrl = null;
            string duration = null;
            string category = null;
            string domain = null;

            // Name heuristics
            var titleNode = node.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//a");
            if (titleNode != null)
                name = NormalizeText(titleNode.InnerText);

            // Description heuristics
            var descNode = node.SelectSingleNode(".//p") ?? node.SelectSingleNode(".//div[contains(@class,'description')]");
            if (descNode != null)
                description = NormalizeText(descNode.InnerText);

            // Source link
            var link = node.SelectSingleNode(".//a[@href]") ?? node.SelectSingleNode(".//a[contains(text(),'Details') or contains(text(),'Read')]");
            if (link != null)
            {
                var href = link.GetAttributeValue("href", null);
                if (!string.IsNullOrWhiteSpace(href))
                {
                    sourceUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : new Uri(new Uri(CoursesUrl), href).ToString();
                }
            }

            var modules = new List<Module>();

            // If there's a detail page, try to fetch modules/duration from it
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var detailHtml = await client.GetStringAsync(sourceUrl);
                    var detailDoc = new HtmlDocument();
                    detailDoc.LoadHtml(detailHtml);

                    // Find duration text
                    var durationNode = detailDoc.DocumentNode.SelectSingleNode("//p[contains(translate(.,'DURATION','duration'),'Duration')]|//span[contains(translate(.,'DURATION','duration'),'Duration')]")
                                       ?? detailDoc.DocumentNode.SelectSingleNode("//li[contains(translate(.,'DURATION','duration'),'Duration')]");
                    if (durationNode != null)
                        duration = NormalizeText(durationNode.InnerText);

                    // Find category/domain
                    var catNode = detailDoc.DocumentNode.SelectSingleNode("//h2[contains(translate(.,'Domain','domain'),'Domain')]|//p[contains(translate(.,'Category','category'),'Category')]");
                    if (catNode != null)
                        category = NormalizeText(catNode.InnerText);
                        domain = category;

                    // Find modules/syllabus: look for headings followed by ul/li
                    var moduleContainers = detailDoc.DocumentNode.SelectNodes("//h2[contains(translate(.,'MODULE','module'),'module')]|//h3[contains(translate(.,'MODULE','module'),'module')]|//div[contains(@class,'syllabus')]")
                                           ?? Enumerable.Empty<HtmlNode>();

                    foreach (var mc in moduleContainers)
                    {
                        // find next sibling list
                        var list = mc.SelectSingleNode("following-sibling::ul[1]") ?? mc.SelectSingleNode(".//ul");
                        if (list != null)
                        {
                            int order = 1;
                            foreach (var li in list.SelectNodes(".//li") ?? Enumerable.Empty<HtmlNode>())
                            {
                                var mname = NormalizeText(li.SelectSingleNode(".//b")?.InnerText ?? li.SelectSingleNode(".//strong")?.InnerText ?? li.InnerText);
                                if (string.IsNullOrWhiteSpace(mname))
                                    continue;
                                modules.Add(new Module { ModuleName = mname, ModuleDescription = NormalizeText(li.InnerText), ModuleOrder = order++ });
                            }
                        }
                    }

                    // Fallback: any list items in the page that look like modules
                    if (!modules.Any())
                    {
                        var lists = detailDoc.DocumentNode.SelectNodes("//ul");
                        if (lists != null)
                        {
                            foreach (var l in lists)
                            {
                                foreach (var li in l.SelectNodes(".//li") ?? Enumerable.Empty<HtmlNode>())
                                {
                                    var text = NormalizeText(li.InnerText);
                                    if (text.Length > 3 && text.Length < 300)
                                    {
                                        modules.Add(new Module { ModuleName = text, ModuleDescription = text, ModuleOrder = modules.Count + 1 });
                                    }
                                }
                            }
                        }
                    }

                    // If we still don't have name, attempt to get from detail page title
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        var dt = detailDoc.DocumentNode.SelectSingleNode("//h1|//h2|//title");
                        if (dt != null)
                            name = NormalizeText(dt.InnerText);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch detail page {Url}", sourceUrl);
                }
            }

            var course = new Course
            {
                CourseName = name?.Trim(),
                Description = description,
                Category = category,
                Domain = domain ?? category,
                Duration = duration,
                SourceUrl = sourceUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Modules = modules
            };

            return (course, modules);
        }

        private string NormalizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
            var t = HtmlEntity.DeEntitize(input).Trim();
            return System.Text.RegularExpressions.Regex.Replace(t, "\\s+", " ");
        }
    }
}
