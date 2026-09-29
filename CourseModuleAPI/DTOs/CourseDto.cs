using System;
using System.Collections.Generic;

namespace CourseModuleAPI.DTOs
{
    public class CourseDto
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Duration { get; set; }
        public string SourceUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ModuleDto> Modules { get; set; } = new List<ModuleDto>();
    }
}
