using Resume.Entities;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;


namespace Resume.Service
{
    public class GeminiService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public GeminiService(IConfiguration config)
        {
            _config = config;
            _httpClient = new HttpClient();
        }

        public async Task<ResumeEntity> AnalyzeResumeAsync(string resumeText)
        {
            //  if the text is too short or lacks typical resume keywords, flag as likely not a resume
            if (string.IsNullOrWhiteSpace(resumeText) || resumeText.Length < 200 || !resumeText.ToLower().Contains("experience") && !resumeText.ToLower().Contains("education") && !resumeText.ToLower().Contains("skills"))
            {
                return new ResumeEntity
                {
                    Name = "Check your resume again, I think you uploaded a wrong data in a hurry.",
                    ResumeText = resumeText,
                    SkillsSummary = string.Empty,
                    Strengths = string.Empty,
                    Weaknesses = string.Empty,
                    Suggestions = string.Empty,
                    SuggestedRoles = string.Empty
                };
            }

            var apiKey = _config["Gemini"];
            var prompt = $@"
You are a resume assistant. Review the following resume and extract the following in short bullet form.

1. Name
2. Key Skills (max 5)
3. Strengths (max 3)
4. Weaknesses (max 2)
5. Suggestions (max 2)
6. Suggested Roles (max 3)

Use this format exactly:

Name: [Full Name]

Skills Summary:
- Skill 1
- Skill 2
...

Strengths:
- Point 1
...

Weaknesses:
- Point 1

Suggestions:
- Point 1

Suggested Roles:
- Role 1

Rules:
- Do not repeat any section
- Each bullet point must start with ' - '
- Keep each point concise(max 1 sentence)
- Do not include explanations
- Avoid long paragraphs or duplicate headings

Resume:
{resumeText} ";

            //    Create API Payload

            var requestPayload = new
            {
                contents = new[]
                 {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            // Passes your prompt + API key
            var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);



            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {(int)response.StatusCode} {response.ReasonPhrase}. {errorContent}");
            }


            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var aiOutput = json.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return ParseFeedback(aiOutput, resumeText);
        }

        private ResumeEntity ParseFeedback(string response, string resumeText)
        {
            // Split lines from the AI output
            var lines = response.Replace("\r", "").Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

            string[] headers = {
                "Skills Summary:",
                "Strengths:",
                "Weaknesses:",
                "Suggestions:",
                "Suggested Roles:"
            };


            // Extracts all lines between a given section header and the next section header
            string ExtractBlock(string key, string[] allHeaders)
            {
                var sb = new StringBuilder();
                bool recording = false;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Stop if we reach a new section (but not the current key itself)
                    if (allHeaders.Any(h => trimmed.StartsWith(h, StringComparison.OrdinalIgnoreCase)) &&
                        !trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (recording) break;
                    }

                    if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        recording = true;
                        continue;
                    }

                    if (recording) sb.AppendLine(trimmed);
                }

                return sb.ToString().Trim();
            }
            // name
            string nameLine = lines.FirstOrDefault(l => l.StartsWith("Name:", StringComparison.OrdinalIgnoreCase));
            var name = nameLine != null ? nameLine.Replace("Name:", "", StringComparison.OrdinalIgnoreCase).Trim() : "Not found";

            return new ResumeEntity
            {
                ResumeText = resumeText,
                Name = name,
                SkillsSummary = ExtractBlock("Skills Summary:", headers),
                Strengths = ExtractBlock("Strengths:", headers),
                Weaknesses = ExtractBlock("Weaknesses:", headers),
                Suggestions = ExtractBlock("Suggestions:", headers),
                SuggestedRoles = ExtractBlock("Suggested Roles:", headers)
            };
        }

    }
}
