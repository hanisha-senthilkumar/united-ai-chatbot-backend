using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseModuleAPI.Models
{
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }

        [Required]
        [MaxLength(500)]
        public string ModuleName { get; set; }

        public string ModuleDescription { get; set; }

        public int ModuleOrder { get; set; }

        [ForeignKey("Course")]
        public int CourseId { get; set; }
        public Course Course { get; set; }
    }
}
