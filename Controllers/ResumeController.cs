using Microsoft.AspNetCore.Mvc;
using Resume.Data;
using Resume.Entities;
using Resume.Service;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Text;

namespace Resume.Controllers
{
    public class ResumeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _geminiService;

        public ResumeController(ApplicationDbContext context, GeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        [HttpGet]
        public IActionResult Upload() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Upload(IFormFile file, string resumeText, string inputType)
        {
            // Remove default model binding errors for file and resumeText
            ModelState.Remove("file");
            ModelState.Remove("resumeText");

            // Validation: ensure at least one input is provided based on inputType
            if ((inputType == "file" && (file == null || file.Length == 0)) || (inputType == "text" && string.IsNullOrWhiteSpace(resumeText)))
            {
                ModelState.AddModelError("", inputType == "file" ? "Please upload a file." : "Please paste your resume text.");
                return View();
            }

            if (inputType == "text")
            {
                // If user pasted text, use it directly
                try
                {
                    var resumeEntity = await _geminiService.AnalyzeResumeAsync(resumeText);
                    if (resumeEntity.Name.StartsWith("Check your resume again"))
                    {
                        ModelState.AddModelError("", resumeEntity.Name);
                        return View();
                    }
                    resumeEntity = FixGeminiOutputFormatting(resumeEntity);
                    resumeEntity.UploadedFileName = "(Pasted Text)";
                    if (string.IsNullOrWhiteSpace(resumeEntity.Name) || resumeEntity.Name.Trim().ToLower() == "not found")
                    {
                        resumeEntity.Name = ExtractNameFromResume(resumeText) ?? "(Pasted Text)";
                    }
                    return View(resumeEntity);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"AI analysis failed: {ex.Message}");
                    return View();
                }
            }

            if (file == null || file.Length == 0) return View();

            string resumeTextFromFile;
            var ext = Path.GetExtension(file.FileName).ToLower();

            using var stream = file.OpenReadStream();

            if (ext == ".pdf")
            {
                using var pdf = PdfDocument.Open(stream);
                resumeTextFromFile = string.Join("\n", pdf.GetPages().Select(p => p.Text)); // Extracts the text from each page and joins them with newline characters.
            }
            else if (ext == ".docx")
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                using var wordDoc = WordprocessingDocument.Open(ms, false);
                var body = wordDoc.MainDocumentPart.Document.Body;
                resumeTextFromFile = body.InnerText;
            }
            else if (ext == ".txt")
            {
                using var reader = new StreamReader(stream);
                resumeTextFromFile = await reader.ReadToEndAsync();
            }
            else
            {
                ModelState.AddModelError("", "Unsupported file format. Use PDF, DOCX, or TXT.");
                return View();
            }

            try
            {
                var resumeEntity = await _geminiService.AnalyzeResumeAsync(resumeTextFromFile);
                if (resumeEntity.Name.StartsWith("Check your file again"))
                {
                    ModelState.AddModelError("", resumeEntity.Name);
                    return View();
                }
                // Post-process the AI output for correct formatting
                resumeEntity = FixGeminiOutputFormatting(resumeEntity);
                resumeEntity.UploadedFileName = file.FileName;
                // Fallback: If name is empty or 'Not found', try to extract from resume text
                if (string.IsNullOrWhiteSpace(resumeEntity.Name) || resumeEntity.Name.Trim().ToLower() == "not found")
                {
                    resumeEntity.Name = ExtractNameFromResume(resumeTextFromFile) ?? file.FileName;
                }
                return View(resumeEntity);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"AI analysis failed: {ex.Message}");
                return View();
            }
        }

        // Post-processing function for Gemini output formatting
        private ResumeEntity FixGeminiOutputFormatting(ResumeEntity entity)
        {
            string[] headers = {
                "Name",
                "Skills Summary",
                "Strengths",
                "Weaknesses",
                "Suggestions",
                "Suggested Roles"
            };

            entity.SkillsSummary = FixSectionFormatting(entity.SkillsSummary, "Skills Summary");
            entity.Name = FixSectionFormatting(entity.Name, "Name");
            entity.Strengths = FixSectionFormatting(entity.Strengths, "Strengths");
            entity.Weaknesses = FixSectionFormatting(entity.Weaknesses, "Weaknesses");
            entity.Suggestions = FixSectionFormatting(entity.Suggestions, "Suggestions");
            entity.SuggestedRoles = FixSectionFormatting(entity.SuggestedRoles, "Suggested Roles");
            return entity;
        }

        private string FixSectionFormatting(string section, string header)
        {
            if (string.IsNullOrWhiteSpace(section)) return section;

            section = Regex.Replace(section, @"(?<=[^\n])-\s*", "\n-"); // Ensures each bullet - starts on a new line

            // Removes any duplicate header
            section = Regex.Replace(section, $"{Regex.Escape(header)}:\\s*", "", RegexOptions.IgnoreCase);

            return section.Trim();
        }


        // Fallback: Try to extract name from resume text
        private string ExtractNameFromResume(string resumeText)
        {
            if (string.IsNullOrWhiteSpace(resumeText)) return null;
            var lines = resumeText.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = trimmed.Substring(5).Trim(); // remove name:
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
                // If the first non-empty line looks like a name (no numbers, not too long)
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length < 50 && !trimmed.Any(char.IsDigit) && !trimmed.Contains("@"))
                {
                    return trimmed;
                }
            }
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Save(ResumeEntity model)
        {
            _context.Resumes.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            var data = _context.Resumes.OrderByDescending(r => r.CreatedAt).ToList();
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public IActionResult DownloadDocxTemp(ResumeEntity model)
        {
            using var ms = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();
                body.Append(new Paragraph(new Run(new Text("AI Resume Review")))
                {
                    ParagraphProperties = new ParagraphProperties(new Justification() { Val = JustificationValues.Center })
                });
                body.Append(new Paragraph(new Run(new Text("Name:"))));
                body.Append(new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(model.Name) ? model.UploadedFileName : model.Name ?? ""))));
                body.Append(new Paragraph(new Run(new Text("Skills Summary:"))));
                body.Append(new Paragraph(new Run(new Text(model.SkillsSummary ?? ""))));
                body.Append(new Paragraph(new Run(new Text("Strengths:"))));
                body.Append(new Paragraph(new Run(new Text(model.Strengths ?? ""))));
                body.Append(new Paragraph(new Run(new Text("Weaknesses:"))));
                body.Append(new Paragraph(new Run(new Text(model.Weaknesses ?? ""))));
                body.Append(new Paragraph(new Run(new Text("Suggestions:"))));
                body.Append(new Paragraph(new Run(new Text(model.Suggestions ?? ""))));
                body.Append(new Paragraph(new Run(new Text("Suggested Roles:"))));
                body.Append(new Paragraph(new Run(new Text(model.SuggestedRoles ?? ""))));
                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }
            ms.Position = 0;
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "AI-Resume-Feedback.docx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var resume = await _context.Resumes.FindAsync(id);
            if (resume != null)
            {
                _context.Resumes.Remove(resume);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("List");
        }

    }
}
