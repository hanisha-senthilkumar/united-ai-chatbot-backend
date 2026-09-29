using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CourseModuleAPI.Models;
using Xunit;

namespace CourseModuleAPI.Tests.Models
{
    public class CourseModelTests
    {
        private IList<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void ValidCourse_PassesValidation()
        {
            var c = new Course
            {
                CourseName = "Test Course",
                Description = "A sample course",
                Category = "Programming",
                Duration = "4 weeks",
                SourceUrl = "https://example.com/course/test"
            };

            var results = Validate(c);
            Assert.Empty(results);
        }

        [Fact]
        public void MissingCourseName_FailsValidation()
        {
            var c = new Course
            {
                CourseName = null,
                Description = "No name",
            };

            var results = Validate(c);
            Assert.Contains(results, r => r.MemberNames != null && System.Linq.Enumerable.Contains(r.MemberNames, "CourseName"));
        }

        [Fact]
        public void TooLongCourseName_FailsValidation()
        {
            var longName = new string('A', 600);
            var c = new Course
            {
                CourseName = longName
            };

            var results = Validate(c);
            Assert.Contains(results, r => r.MemberNames != null && System.Linq.Enumerable.Contains(r.MemberNames, "CourseName"));
        }
    }
}
