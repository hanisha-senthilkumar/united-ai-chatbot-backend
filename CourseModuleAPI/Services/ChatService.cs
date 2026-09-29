using System;
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
                // List courses
                if (q.IndexOf("what courses", StringComparison.OrdinalIgnoreCase) >= 0 || q.IndexOf("courses available", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var courses = await _courseService.GetAllAsync();
                    var names = string.Join(", ", courses.Select(c => c.CourseName));
                    resp.Success = true;
                    resp.Answer = string.IsNullOrWhiteSpace(names) ? "No courses found." : $"Available courses: {names}.";
                    return resp;
                }

                // Identify a course name mentioned in the query by checking known course names
                var allCourses = await _courseService.GetAllAsync();
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
                resp.Success = false;
                resp.Answer = "I can help you with course-related information such as course details, duration, modules, and topics. Please ask about a specific course or ask 'What courses are available?'.";
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

        private static bool IsQueryStopWord(string value)
        {
            return value.Equals("the", StringComparison.OrdinalIgnoreCase)
                || value.Equals("course", StringComparison.OrdinalIgnoreCase)
                || value.Equals("what", StringComparison.OrdinalIgnoreCase)
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
                || value.Equals("available", StringComparison.OrdinalIgnoreCase);
        }
    }
}
