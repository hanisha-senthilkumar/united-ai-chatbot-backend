using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseModuleAPI.Models
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(500)]
        public string CourseName { get; set; }

        public string Description { get; set; }

        [MaxLength(200)]
        public string Category { get; set; }

        [MaxLength(100)]
        public string Duration { get; set; }

        [MaxLength(2000)]
        public string SourceUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}
