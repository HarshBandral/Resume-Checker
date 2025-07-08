using System.ComponentModel.DataAnnotations;

namespace Resume.Entities
{
    public class ResumeEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string ResumeText { get; set; }
        public string SkillsSummary { get; set; }
        public string Strengths { get; set; }
        public string Weaknesses { get; set; }
        public string Suggestions { get; set; }
        public string SuggestedRoles { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UploadedFileName { get; set; }
    }
}
