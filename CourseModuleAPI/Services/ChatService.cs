using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CourseModuleAPI.DTOs;
using CourseModuleAPI.Interfaces;
using Microsoft.Extensions.Logging;

namespace CourseModuleAPI.Services
{
    public class ChatService : IChatService
    {
        private readonly ICourseService _courseService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ICourseService courseService, ILogger<ChatService> logger)
        {
            _courseService = courseService;
            _logger = logger;
        }

        public async Task<ChatResponse> ProcessQueryAsync(ChatRequest request)
        {
            var resp = new ChatResponse { Success = false, Query = request?.Query };
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                resp.Answer = "Please provide a course-related question in the 'query' field.";
                return resp;
            }

            _logger.LogInformation("Processing chat query: {Query}", request.Query);

            var q = request.Query.Trim();

            // Simple intent detection: check keywords
            try
            {
                var allCourses = (await _courseService.GetAllAsync()).ToList();
                var domain = ExtractDomain(q, allCourses);
                if (domain != null)
                {
                    var domainCourses = (await _courseService.GetByDomainAsync(domain)).ToList();
                    if (IsAvailabilityQuery(q))
                    {
                        resp.Success = true;
                        resp.Answer = $"Yes, the {domain} domain is available with {domainCourses.Count} courses.";
                        resp.Sources.AddRange(domainCourses.Select(c => new { courseId = c.CourseId, courseName = c.CourseName }));
                        return resp;
                    }

                    var isCountQuery = q.IndexOf("how many", StringComparison.OrdinalIgnoreCase) >= 0
                        || q.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0
                        || q.IndexOf("number of", StringComparison.OrdinalIgnoreCase) >= 0;

                    resp.Success = true;
                    resp.Answer = !domainCourses.Any()
                        ? "No courses found for the requested domain."
                        : isCountQuery
                            ? $"There are {domainCourses.Count} courses in the {domain} domain."
                            : $"Courses in the {domain} domain: {string.Join(", ", domainCourses.Select(c => c.CourseName))}.";
                    resp.Sources.AddRange(domainCourses.Select(c => new { courseId = c.CourseId, courseName = c.CourseName }));
                    return resp;
                }

                // List courses
                if (q.IndexOf("what courses", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("courses available", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (TryExtractRequestedDomain(q, out _))
                    {
                        resp.Success = true;
                        resp.Answer = "No courses found for the requested domain.";
                        return resp;
                    }

                    var names = string.Join(", ", allCourses.Select(c => c.CourseName));
                    resp.Success = true;
                    resp.Answer = string.IsNullOrWhiteSpace(names) ? "No courses found." : $"Available courses: {names}.";
                    return resp;
                }

                // Identify a course name mentioned in the query by checking known course names
                var matched = allCourses.FirstOrDefault(c => q.IndexOf(c.CourseName, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matched == null)
                {
                    // try partial match by individual words
                    foreach (var c in allCourses)
                    {
                        var parts = c.CourseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            if (p.Length < 3 || IsQueryStopWord(p)) continue;
                            if (Regex.IsMatch(q, $@"\b{Regex.Escape(p)}\b", RegexOptions.IgnoreCase))
                            {
                                matched = c; break;
                            }
                        }
                        if (matched != null) break;
                    }
                }

                if (matched != null)
                {
                    resp.Course = matched.CourseName;
                    if (IsAvailabilityQuery(q))
                    {
                        resp.Success = true;
                        resp.Answer = $"Yes, {matched.CourseName} is currently available.";
                        resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                        return resp;
                    }

                    // Intent: duration. Keep this database-backed: the answer always uses matched.Duration.
                    if (IsDurationQuery(q))
                    {
                        resp.Success = true;
                        var dur = string.IsNullOrWhiteSpace(matched.Duration) ? "Duration information is not available." : matched.Duration;
                        resp.Answer = string.IsNullOrWhiteSpace(matched.Duration)
                            ? $"Duration information is not available for the {matched.CourseName} course."
                            : $"The duration of the {matched.CourseName} course is {dur}.";
                        resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                        return resp;
                    }

                    // Intent: modules
                    if (q.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("syllabus", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("topics", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var modules = (await _courseService.GetModulesByCourseIdAsync(matched.CourseId)).ToList();
                        if (!modules.Any())
                        {
                            resp.Success = true;
                            resp.Answer = $"I couldn't find module details for the {matched.CourseName} course.";
                            resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                            return resp;
                        }

                        var sb = new StringBuilder();
                        sb.AppendLine($"The {matched.CourseName} course covers the following modules:");
                        int i = 1;
                        foreach (var m in modules)
                        {
                            sb.AppendLine($"{i}. {m.ModuleName}");
                            i++;
                        }

                        resp.Success = true;
                        resp.Answer = sb.ToString();
                        resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                        return resp;
                    }

                    // Intent: full details
                    if (q.IndexOf("tell me about", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("details", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("what is", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Course: {matched.CourseName}");
                        if (!string.IsNullOrWhiteSpace(matched.Description)) sb.AppendLine($"Description: {matched.Description}");
                        if (!string.IsNullOrWhiteSpace(matched.Category)) sb.AppendLine($"Category: {matched.Category}");
                        if (!string.IsNullOrWhiteSpace(matched.Duration)) sb.AppendLine($"Duration: {matched.Duration}");

                        var modules = (await _courseService.GetModulesByCourseIdAsync(matched.CourseId)).ToList();
                        if (modules.Any())
                        {
                            sb.AppendLine("Modules:");
                            int i = 1;
                            foreach (var m in modules)
                            {
                                sb.AppendLine($"{i}. {m.ModuleName} - {m.ModuleDescription}");
                                i++;
                            }
                        }

                        resp.Success = true;
                        resp.Answer = sb.ToString();
                        resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                        return resp;
                    }

                    // Default: give summary
                    resp.Success = true;
                    resp.Answer = $"I found the {matched.CourseName} course. Ask about duration, modules, or details.";
                    resp.Sources.Add(new { courseId = matched.CourseId, courseName = matched.CourseName });
                    return resp;
                }

                if (IsDomainQuery(q) && TryExtractRequestedDomain(q, out _))
                {
                    resp.Success = true;
                    resp.Answer = "No courses found for the requested domain.";
                    return resp;
                }

                if (IsCourseAvailabilityQuery(q) && TryExtractRequestedCourse(q, out var requestedCourse))
                {
                    resp.Success = true;
                    resp.Answer = $"No, {requestedCourse} courses are not currently available. You can ask about any of the available courses.";
                    return resp;
                }

                var relatedCourses = FindRelatedCourses(q, allCourses).ToList();
                if (relatedCourses.Any())
                {
                    resp.Success = true;
                    resp.Answer = BuildRelatedAnswer(q, relatedCourses);
                    resp.Sources.AddRange(relatedCourses.Select(c => new { courseId = c.CourseId, courseName = c.CourseName }));
                    return resp;
                }

                if (IsDurationQuery(q))
                {
                    resp.Answer = "Please specify the course name so I can retrieve its duration.";
                    return resp;
                }

                // If no course matched, but query asks for course list
                if (q.IndexOf("which course", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("which courses", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var courses = await _courseService.GetAllAsync();
                    var names = string.Join(", ", courses.Select(c => c.CourseName));
                    resp.Success = true;
                    resp.Answer = string.IsNullOrWhiteSpace(names) ? "No courses found." : $"Available courses: {names}.";
                    return resp;
                }

                // Unknown / fallback
                resp.Success = true;
                resp.Answer = "I couldn't find that course or related information in the available course data. Please ask about another course or domain.";
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat query");
                resp.Success = false;
                resp.Answer = "An error occurred while processing your request.";
                return resp;
            }
        }

        private static bool IsDurationQuery(string query)
        {
            return query.IndexOf("duration", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("how long", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("how many month", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("how many week", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("course length", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAvailabilityQuery(string query)
        {
            return query.IndexOf("is there", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("available", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("availability", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("do we have", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCourseAvailabilityQuery(string query)
        {
            return IsAvailabilityQuery(query)
                || query.IndexOf("course", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<CourseDto> FindRelatedCourses(string query, IEnumerable<CourseDto> courses)
        {
            var terms = ExtractSearchTerms(query).ToList();
            if (!terms.Any())
                return Enumerable.Empty<CourseDto>();

            return courses
                .Select(course => new
                {
                    Course = course,
                    Score = terms.Sum(term => SearchableCourseText(course).Contains(term, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                })
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Course.CourseName)
                .Take(5)
                .Select(result => result.Course);
        }

        private static string BuildRelatedAnswer(string query, IEnumerable<CourseDto> courses)
        {
            var names = courses.Select(c => c.CourseName).ToList();
            if (IsAvailabilityQuery(query))
                return $"Yes, a related course is available: {string.Join(", ", names)}.";

            return $"I found related courses in the available data: {string.Join(", ", names)}.";
        }

        private static string SearchableCourseText(CourseDto course)
        {
            return string.Join(" ", new[]
            {
                course.CourseName,
                course.Domain,
                course.Category,
                course.Description,
                string.Join(" ", course.Modules?.Select(m => $"{m.ModuleName} {m.ModuleDescription}") ?? Enumerable.Empty<string>())
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static IEnumerable<string> ExtractSearchTerms(string query)
        {
            return Regex.Matches(query.ToLowerInvariant(), @"[a-z][a-z0-9+#.-]*")
                .Cast<Match>()
                .Select(match => match.Value.Trim('.', '-', '+', '#'))
                .Where(term => term.Length >= 2 && !IsQueryStopWord(term));
        }

        private static bool TryExtractRequestedCourse(string query, out string course)
        {
            var match = Regex.Match(query,
                @"(?:is there|do we have|do you provide|is|provide)\s+(?:an?\s+)?(?<course>[A-Za-z][A-Za-z0-9+#.-]*(?:\s+[A-Za-z][A-Za-z0-9+#.-]*){0,3})\s+courses?\b",
                RegexOptions.IgnoreCase);

            course = match.Success ? match.Groups["course"].Value.Trim() : null;
            return match.Success && !string.IsNullOrWhiteSpace(course);
        }

        private static bool IsDomainQuery(string query)
        {
            return query.IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("how many", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf(" in ", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("show courses", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("what courses", StringComparison.OrdinalIgnoreCase) >= 0
                || query.IndexOf("courses available", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractDomain(string query, IEnumerable<CourseDto> courses)
        {
            var domains = courses
                .Select(c => c.Domain)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(d => d.Length)
                .ToList();

            foreach (var domain in domains)
            {
                var pattern = $@"(?<![A-Za-z0-9]){Regex.Escape(domain.Trim())}(?![A-Za-z0-9])";
                if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase))
                    return domain;

                var words = domain.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 1)
                {
                    var acronym = string.Concat(words.Select(word => word[0]));
                    if (Regex.IsMatch(query, $@"\b{Regex.Escape(acronym)}\b", RegexOptions.IgnoreCase))
                        return domain;
                }
            }

            return null;
        }

        private static bool TryExtractRequestedDomain(string query, out string domain)
        {
            var match = Regex.Match(query,
                @"(?:\b(?:in|for|from)\s+(?:the\s+)?|\b(?:is there|do we have)\s+)(?<domain>[A-Za-z][A-Za-z0-9]*(?:\s+[A-Za-z][A-Za-z0-9]*){0,3})\s*(?:domain|courses?)?\b",
                RegexOptions.IgnoreCase);

            domain = match.Success ? match.Groups["domain"].Value.Trim() : null;
            return match.Success && !string.IsNullOrWhiteSpace(domain);
        }

        private static bool IsQueryStopWord(string value)
        {
            return value.Equals("the", StringComparison.OrdinalIgnoreCase)
                || value.Equals("course", StringComparison.OrdinalIgnoreCase)
                || value.Equals("what", StringComparison.OrdinalIgnoreCase)
                || value.Equals("is", StringComparison.OrdinalIgnoreCase)
                || value.Equals("there", StringComparison.OrdinalIgnoreCase)
                || value.Equals("are", StringComparison.OrdinalIgnoreCase)
                || value.Equals("an", StringComparison.OrdinalIgnoreCase)
                || value.Equals("any", StringComparison.OrdinalIgnoreCase)
                || value.Equals("available", StringComparison.OrdinalIgnoreCase)
                || value.Equals("currently", StringComparison.OrdinalIgnoreCase)
                || value.Equals("which", StringComparison.OrdinalIgnoreCase)
                || value.Equals("this", StringComparison.OrdinalIgnoreCase)
                || value.Equals("that", StringComparison.OrdinalIgnoreCase)
                || value.Equals("how", StringComparison.OrdinalIgnoreCase)
                || value.Equals("long", StringComparison.OrdinalIgnoreCase)
                || value.Equals("many", StringComparison.OrdinalIgnoreCase)
                || value.Equals("month", StringComparison.OrdinalIgnoreCase)
                || value.Equals("months", StringComparison.OrdinalIgnoreCase)
                || value.Equals("week", StringComparison.OrdinalIgnoreCase)
                || value.Equals("weeks", StringComparison.OrdinalIgnoreCase)
                || value.Equals("duration", StringComparison.OrdinalIgnoreCase)
                || value.Equals("information", StringComparison.OrdinalIgnoreCase);
        }
    }
}
